using EMG.Api.Modules.Advances.Services;
using EMG.Api.Modules.FinalAccounts.Dtos;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using EMG.Api.Modules.UniversalImports;

namespace EMG.Api.Modules.FinalAccounts.Services;

public sealed class FinalAccountsService(EmgDbContext db) : IFinalAccountsService
{
    public async Task<FinalAccountSummary?> GetAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null) return null;
        if (!isAdmin && (userId is null || !await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == projectId && item.UserId == userId && item.IsActive, cancellationToken))) return null;

        // Reports and balances are based only on approved/posted source rows.
        // Draft and review rows remain visible in workflow screens and cannot
        // affect the financial truth until an authorized approval occurs.
        var postedSite = db.SiteMovements.Where(item => item.ProjectId == projectId && item.Currency == "EGP" && (item.Status == "معتمد" || item.Status == "مرحّل"));
        var totalExpenses = await postedSite.Where(item => item.MovementType != "وارد").SumAsync(item => item.TotalAmount, cancellationToken)
            + await db.SupplierPayments.Where(item => item.ProjectId == projectId && item.PaymentSource != "مالك مباشر" && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.Amount, cancellationToken)
            + await db.ContractorPayments.Where(item => item.ProjectId == projectId && item.PaymentSource != "مالك مباشر" && item.Currency == "EGP").SumAsync(item => item.Amount, cancellationToken);
        var supplierSupplies = await db.SupplierSupplies.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.TotalAmount, cancellationToken);
        var supplierPaid = await db.SupplierPayments.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.Amount, cancellationToken);
        var historicalSupplierWorks = await db.HistoricalWorkLines.Where(w => w.Batch.ProjectId == projectId && w.SupplierId != null && w.Currency == "EGP").SumAsync(w => w.Amount,cancellationToken);
        var historicalSupplierPaid = await db.HistoricalSettlements.Where(s => s.Batch.ProjectId == projectId && s.SupplierId != null && s.Currency == "EGP").SumAsync(s => s.Amount,cancellationToken);
        var supplierOutstanding = supplierSupplies + historicalSupplierWorks - supplierPaid - historicalSupplierPaid;
        var outgoingByCategory = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var history = db.HistoricalWorkLines.AsNoTracking().Where(w => w.Batch.ProjectId == projectId);
        var unlinked = await history.Where(w => w.OperationalId == null)
            .GroupBy(w => new { w.Currency, w.Category })
            .Select(g => new { g.Key.Currency, g.Key.Category, Amount = g.Sum(w => w.Amount) }).ToListAsync(cancellationToken);
        void AddOutgoing(string category, decimal amount) => outgoingByCategory[category] = outgoingByCategory.GetValueOrDefault(category) + amount;
        AddOutgoing("الموقع", await postedSite.Where(item => item.MovementType != "وارد").SumAsync(item => item.TotalAmount, cancellationToken));
        // Canonical financial buckets are independent from the detailed
        // operational section.  In particular, imported concrete and
        // finishing rows historically entered with the entity default
        // "المقاول"; that is not the source workbook's final-account bucket.
        // Normalize imported legacy rows at read time so existing projects
        // are corrected globally without a destructive data rewrite.
        var excavationTotal = await db.ExcavationRecords.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.TotalAmount, cancellationToken);
        AddOutgoing("العرب", excavationTotal);
        var concreteRows = await db.ConcreteRecords.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل"))
            .Select(item => new { item.AccountCategory, item.TotalAmount, item.Notes }).ToListAsync(cancellationToken);
        foreach (var item in concreteRows)
            AddOutgoing(CanonicalCategory("الخرسانات", item.AccountCategory, item.Notes), item.TotalAmount);
        foreach (var item in await db.SteelRecords.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).GroupBy(item => item.AccountCategory).Select(group => new { Category = group.Key, Amount = group.Sum(item => item.TotalAmount) }).ToListAsync(cancellationToken)) AddOutgoing(item.Category, item.Amount);
        var finishingRows = await db.FinishingRecords.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل"))
            .Select(item => new { item.AccountCategory, item.TotalAmount, item.Notes, RecoveredCategory = history
                .Where(w => w.OperationalTable == "FinishingRecord" && w.OperationalId == item.Id).Select(w => w.Category).FirstOrDefault() }).ToListAsync(cancellationToken);
        foreach (var item in finishingRows)
            AddOutgoing(item.RecoveredCategory ?? CanonicalCategory("التشطيبات", item.AccountCategory, item.Notes), item.TotalAmount);
        foreach (var work in unlinked.Where(w => w.Currency == "EGP")) AddOutgoing(work.Category, work.Amount);
        var incomingByCategory = await db.AdvanceAllocations.Where(item => item.AdvancePayment.ProjectId == projectId && item.AdvancePayment.Currency == "EGP" && (item.AdvancePayment.Status == "معتمد" || item.AdvancePayment.Status == "مرحّل")).GroupBy(item => item.AccountCategory).Select(group => new { Category = group.Key, Amount = group.Sum(item => item.Amount) }).ToDictionaryAsync(item => item.Category, item => item.Amount, StringComparer.Ordinal, cancellationToken);
        var funding = await db.HistoricalFundingLines.AsNoTracking().Where(f => f.Batch.ProjectId == projectId && f.Currency == "EGP").ToListAsync(cancellationToken);
        var basis = new HistoricalFundingBasis(funding);
        var ownershipConflict = funding.Any(f => f.Basis == "conflicting_ownership");
        var disputedCategories = funding.Where(f => f.Basis == "project_allocation" && basis.Disputed(f))
            .Select(f => FundingCategory(f.Category)).ToHashSet();
        foreach (var line in funding.Where(basis.Allocates))
        {
            var category = FundingCategory(line.Category);
            incomingByCategory[category] = incomingByCategory.GetValueOrDefault(category) + line.Amount;
        }
        // إجمالي الدفعات تحت الحساب هو ما تم دفعه فعليًا، وليس مجموع التوزيعات
        // على البنود. قد تكون الدفعة موزعة جزئيًا أو يزيد مبلغها عن التوزيع
        // الحالي، وفي الحالتين يجب ألا نفقد قيمتها من كشف الحساب.
        var totalIncoming = await db.AdvancePayments
            .Where(item => item.ProjectId == projectId && item.Currency == "EGP" && (item.Status == "معتمد" || item.Status == "مرحّل"))
            .SumAsync(item => item.Amount, cancellationToken);
        totalIncoming += await postedSite.Where(item => item.MovementType == "وارد").SumAsync(item => item.TotalAmount, cancellationToken);
        totalIncoming += await db.SupplierPayments.Where(item => item.ProjectId == projectId && item.PaymentSource == "مالك مباشر" && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.Amount, cancellationToken);
        totalIncoming += await db.ContractorPayments.Where(item => item.ProjectId == projectId && item.PaymentSource == "مالك مباشر" && item.Currency == "EGP").SumAsync(item => item.Amount, cancellationToken);
        var fundingAdjustments = funding.Where(f => f.Basis == "signed_schedule_adjustment").Sum(f => f.Amount);
        var sourceControl = funding.FirstOrDefault(f => f.Stream == "source_control");
        totalIncoming += funding.Where(basis.AdditionalFunding).Sum(f => f.Amount);
        var totalOutgoing = outgoingByCategory.Values.Sum();
        var officePaid = incomingByCategory.GetValueOrDefault("المكتب");
        var office = OfficeFeeCalculation.Calculate(totalOutgoing, project.OfficeSharePercentage, officePaid);
        var officeShareAmount = office.Fee;
        var netBeforeOfficeShare = totalOutgoing - totalExpenses;
        var netAfterOfficeShare = netBeforeOfficeShare - officeShareAmount;
        var payerBalance = totalOutgoing - totalIncoming;
        var incomplete = await db.HistoricalImportRecords.AnyAsync(record => record.Batch.ProjectId == projectId &&
            record.Batch.Status == HistoricalImportStatuses.Posted &&
            record.Batch.AuditEvents.Any(item => item.Action == HistoricalImportStatuses.UniversalProjectCommitted) &&
            record.LifecycleStatus == HistoricalImportStatuses.ReviewRequired && !record.IsSummary, cancellationToken);
        var status = incomplete ? "غير مكتمل: توجد سجلات استيراد قيد المراجعة" :
            payerBalance > 0 ? "مستحق على الدافع" : payerBalance < 0 ? "رصيد زائد لدى الدافع" : "متزن";
        var cashBalance = decimal.Round(totalIncoming - totalOutgoing, 2);
        var warnings = new List<string>();
        var unknownFunding = ownershipConflict || sourceControl == null && await db.HistoricalImportBatches.AnyAsync(b => b.ProjectId == projectId && b.Status == "POSTED" && b.MappingVersion == "universal-v1.1", cancellationToken)
            && !funding.Any(f => f.Basis == "main_schedule_entry" || f.Basis == "signed_schedule_adjustment");
        if (ownershipConflict)
            warnings.Add($"ورد توزيع بمبلغ {sourceControl?.Amount:N2} ج.م في كشف مطابق لدفعات عميل آخر. لم تتأكد ملكية الدفعة لهذا المشروع.");
        else if (unknownFunding) warnings.Add("لم تثبت قيمة الدفعات التاريخية. تظهر التوزيعات المثبتة لكل بند.");
        var pendingFunding = funding.Where(f => f.Basis == "formula_settlement_review" || f.Basis == "settlement_funding_unconfirmed" || f.Basis == "unresolved_source_note")
            .Sum(f => f.Amount);
        if (pendingFunding != 0) warnings.Add($"يشمل جدول المصدر {pendingFunding:N2} ج.م لم يثبت استلامها مستقلة عن التسويات.");
        var pendingParties = await db.HistoricalSettlements.CountAsync(s => s.Batch.ProjectId == projectId && s.SupplierId == null && s.ContractorId == null,cancellationToken);
        if (pendingParties > 0) warnings.Add($"توجد {pendingParties} تسويات لم يتحدد حساب الطرف المستفيد منها.");
        var workFamilies = await history.Select(w => new { w.Category,w.Family }).Distinct().ToListAsync(cancellationToken);
        var sectionKeys = workFamilies.GroupBy(w => w.Category).ToDictionary(g => g.Key,g => g.Select(w => w.Family switch
        {
            "site" => "movements", "workmanship" or "concrete" => "concrete", "building" or "finishing" => "finishing",
            _ => w.Family
        }).Distinct().ToArray());
        var sections = outgoingByCategory.Keys.Union(incomingByCategory.Keys).Where(category => category != "المكتب")
            .Select(category => new SectionFinancialSummary(category, "EGP", outgoingByCategory.GetValueOrDefault(category),
                incomingByCategory.GetValueOrDefault(category), incomingByCategory.GetValueOrDefault(category) - outgoingByCategory.GetValueOrDefault(category))
                { FundingUnknown = disputedCategories.Contains(category), SectionKey = sectionKeys.TryGetValue(category,out var keys) && keys.Length == 1 ? keys[0] : null })
            .OrderBy(item => item.Category).ToList();
        var transfers = await db.HistoricalFundingLines.Where(f => f.Batch.ProjectId == projectId && f.Basis == "transfer_breakdown")
            .GroupBy(f => f.Currency).Select(g => new { Currency = g.Key, Amount = g.Sum(f => f.Amount) }).ToDictionaryAsync(g => g.Currency,g => g.Amount,cancellationToken);
        var currencies = new List<CurrencyFinancialSummary> { new("EGP", totalOutgoing, unknownFunding && totalIncoming == 0 ? null : totalIncoming, unknownFunding ? null : cashBalance,
            office.Fee, officePaid, office.Outstanding, transfers.GetValueOrDefault("EGP")) };
        var foreignReceipts = db.AdvancePayments.Where(p => p.ProjectId == projectId && p.Currency != "EGP" && (p.Status == "معتمد" || p.Status == "مرحّل"));
        var receipts = await foreignReceipts.Select(p => new { p.Currency, p.Amount })
            .Concat(db.ContractorPayments.Where(p => p.ProjectId == projectId && p.Currency != "EGP" && p.PaymentSource == "مالك مباشر").Select(p => new { p.Currency,p.Amount }))
            .ToListAsync(cancellationToken);
        var site = await db.SiteMovements.Where(p => p.ProjectId == projectId && p.Currency != "EGP" && (p.Status == "معتمد" || p.Status == "مرحّل"))
            .Select(p => new { p.Currency, p.MovementType, p.TotalAmount }).ToListAsync(cancellationToken);
        var allocations = await db.AdvanceAllocations.Where(a => foreignReceipts.Any(p => p.Id == a.AdvancePaymentId))
            .Select(a => new { a.AdvancePayment.Currency, Category = a.AccountCategory, a.Amount })
            .Concat(db.HistoricalFundingLines.Where(f => f.Batch.ProjectId == projectId && f.Currency != "EGP" && f.Basis == "project_allocation")
                .Select(f => new { f.Currency, f.Category, f.Amount })).ToListAsync(cancellationToken);
        foreach (var currency in receipts.Select(p => p.Currency).Union(site.Select(p => p.Currency)).Union(allocations.Select(p => p.Currency))
            .Union(unlinked.Select(p => p.Currency)).Union(transfers.Keys).Where(c => c != "EGP"))
        {
            var costs = unlinked.Where(w => w.Currency == currency).ToDictionary(w => w.Category,w => w.Amount);
            costs["الموقع"] = costs.GetValueOrDefault("الموقع") + site.Where(p => p.Currency == currency && p.MovementType != "وارد").Sum(p => p.TotalAmount);
            var paid = allocations.Where(a => a.Currency == currency).GroupBy(a => FundingCategory(a.Category)).ToDictionary(g => g.Key,g => g.Sum(a => a.Amount));
            var payments = receipts.Where(p => p.Currency == currency).Sum(p => p.Amount) + site.Where(p => p.Currency == currency && p.MovementType == "وارد").Sum(p => p.TotalAmount);
            var works = costs.Values.Sum();
            var officePayments = paid.GetValueOrDefault("المكتب");
            var accrual = OfficeFeeCalculation.Calculate(works,project.OfficeSharePercentage,officePayments);
            currencies.Add(new(currency,works,payments,payments-works,accrual.Fee,officePayments,accrual.Outstanding,transfers.GetValueOrDefault(currency)));
            foreach (var category in costs.Keys.Union(paid.Keys).Where(c => c != "المكتب"))
                sections.Add(new(category,currency,costs.GetValueOrDefault(category),paid.GetValueOrDefault(category),paid.GetValueOrDefault(category)-costs.GetValueOrDefault(category)));
        }

        return new FinalAccountSummary(
            project.Id, project.Name, project.ClientName ?? string.Empty, project.PayerName ?? string.Empty,
            project.OfficeSharePercentage, totalIncoming, totalExpenses, supplierOutstanding, netBeforeOfficeShare,
            officeShareAmount, netAfterOfficeShare, payerBalance, status, incomplete ? "تشخيصي" : "مسودة")
        {
            ProjectCode = project.Code,
            OwnerName = project.OwnerName ?? string.Empty,
            HistoricalFundingAdjustments = fundingAdjustments,
            Sections = sections,
            Currencies = currencies,
            HasRecoveredWork = await history.AnyAsync(cancellationToken),
            HistoricalFundingUnknown = unknownFunding,
            HistoricalFundingBasis = ownershipConflict ? "conflicting_ownership" : sourceControl?.Basis ?? (unknownFunding ? "unknown" :
                funding.Any(f => f.Stream == "main_schedule") ? "main_schedule" : "operational"),
            FinancialWarnings = warnings,
            TotalOutgoing = totalOutgoing,
            TotalWorks = totalOutgoing,
            TotalPayments = unknownFunding && totalIncoming == 0 ? null : totalIncoming,
            CashBalance = unknownFunding ? null : cashBalance,
            CashBalanceStatus = unknownFunding ? "الرصيد غير محدد" : cashBalance > 0 ? "العميل له فلوس" : cashBalance < 0 ? "لنا مستحقات لدى العميل" : "الحساب متوازن",
            OfficePaid = officePaid,
            OfficeNetDue = office.Outstanding,
            HasUnresolvedImportRecords = incomplete,
            OutgoingByCategory = outgoingByCategory,
            IncomingByCategory = incomingByCategory
        };
    }

    private static string FundingCategory(string label)
    {
        var account = System.Text.RegularExpressions.Regex.Replace(label.Trim(), @"^(?:دفعات|دفات|دفعه)\s+(?:تحت\s+)?(?:ال)?حساب\s+", "");
        return account.StartsWith("لل", StringComparison.Ordinal) ? "ال" + account[2..] : account;
    }

    private static string CanonicalCategory(string section, string? storedCategory, string? notes)
    {
        var category = string.IsNullOrWhiteSpace(storedCategory) ? "غير محدد" : storedCategory.Trim();
        var imported = notes?.StartsWith("استيراد", StringComparison.Ordinal) == true;
        if (!imported) return category;
        return section switch
        {
            "الخرسانات" or "التشطيبات" => "باقي المصنعيات",
            _ => category
        };
    }
}
