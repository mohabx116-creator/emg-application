using EMG.Api.Modules.FinalAccounts.Dtos;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

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
        var totalExpenses = await db.SiteMovements.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.TotalAmount, cancellationToken)
            + await db.SupplierPayments.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.Amount, cancellationToken);
        var supplierSupplies = await db.SupplierSupplies.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.TotalAmount, cancellationToken);
        var supplierPaid = await db.SupplierPayments.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.Amount, cancellationToken);
        var supplierOutstanding = supplierSupplies - supplierPaid;
        var outgoingByCategory = new Dictionary<string, decimal>(StringComparer.Ordinal);
        void AddOutgoing(string category, decimal amount) => outgoingByCategory[category] = outgoingByCategory.GetValueOrDefault(category) + amount;
        AddOutgoing("الموقع", await db.SiteMovements.Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل")).SumAsync(item => item.TotalAmount, cancellationToken));
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
            .Select(item => new { item.AccountCategory, item.TotalAmount, item.Notes }).ToListAsync(cancellationToken);
        foreach (var item in finishingRows)
            AddOutgoing(CanonicalCategory("التشطيبات", item.AccountCategory, item.Notes), item.TotalAmount);
        var incomingByCategory = await db.AdvanceAllocations.Where(item => item.AdvancePayment.ProjectId == projectId && (item.AdvancePayment.Status == "معتمد" || item.AdvancePayment.Status == "مرحّل")).GroupBy(item => item.AccountCategory).Select(group => new { Category = group.Key, Amount = group.Sum(item => item.Amount) }).ToDictionaryAsync(item => item.Category, item => item.Amount, StringComparer.Ordinal, cancellationToken);
        // إجمالي الدفعات تحت الحساب هو ما تم دفعه فعليًا، وليس مجموع التوزيعات
        // على البنود. قد تكون الدفعة موزعة جزئيًا أو يزيد مبلغها عن التوزيع
        // الحالي، وفي الحالتين يجب ألا نفقد قيمتها من كشف الحساب.
        var totalIncoming = await db.AdvancePayments
            .Where(item => item.ProjectId == projectId && (item.Status == "معتمد" || item.Status == "مرحّل"))
            .SumAsync(item => item.Amount, cancellationToken);
        var totalOutgoing = outgoingByCategory.Values.Sum();
        var officePaid = incomingByCategory.GetValueOrDefault("المكتب");
        var office = OfficeFeeCalculation.Calculate(totalOutgoing, project.OfficeSharePercentage, officePaid);
        var officeShareAmount = office.Fee;
        var netBeforeOfficeShare = totalOutgoing - totalExpenses;
        var netAfterOfficeShare = netBeforeOfficeShare - officeShareAmount;
        var payerBalance = totalOutgoing - totalIncoming;
        var status = payerBalance > 0 ? "مستحق على الدافع" : payerBalance < 0 ? "رصيد زائد لدى الدافع" : "متزن";

        return new FinalAccountSummary(
            project.Id, project.Name, project.ClientName ?? string.Empty, project.PayerName ?? string.Empty,
            project.OfficeSharePercentage, totalIncoming, totalExpenses, supplierOutstanding, netBeforeOfficeShare,
            officeShareAmount, netAfterOfficeShare, payerBalance, status, "مسودة")
        {
            ProjectCode = project.Code,
            OwnerName = project.OwnerName ?? string.Empty,
            TotalOutgoing = totalOutgoing,
            OfficePaid = officePaid,
            OfficeNetDue = office.Outstanding,
            OutgoingByCategory = outgoingByCategory,
            IncomingByCategory = incomingByCategory
        };
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
