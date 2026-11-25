using System.Text.RegularExpressions;
using EMG.Api.Modules.AccountingClassification.Dtos;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.AccountingClassification.Services;

public sealed class AccountingClassificationService(EmgDbContext db) : IAccountingClassificationService
{
    private static readonly AccountingClassificationCode[] AllCodes = Enum.GetValues<AccountingClassificationCode>();
    private static readonly Regex ImportPattern = new(@"^استيراد(?:\s+دفعة(?:\s+من)?)?\s*:\s*(?<sheet>.+?)\s+صف\s+(?<row>\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SupplierPaymentImportPattern = new(@"^استيراد\s+دفعة\s+من\s+(?<sheet>.+?)\s+صف\s+(?<row>\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<AccountingClassificationPreview?> PreviewAsync(int projectId, int? userId, bool isGlobalAccess, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().Where(item => item.Id == projectId).Select(item => new { item.Id, item.Code, item.Name, item.OfficeSharePercentage }).FirstOrDefaultAsync(cancellationToken);
        if (project is null) return null;
        if (!isGlobalAccess && (userId is null || !await db.UserProjectAssignments.AnyAsync(item => item.ProjectId == projectId && item.UserId == userId && item.IsActive, cancellationToken))) return null;

        var lines = new List<AccountingClassificationLine>();
        var siteMovements = await db.SiteMovements.AsNoTracking().Where(item => item.ProjectId == projectId)
            .Select(item => new RawLine("site-movement", item.Id, item.TotalAmount, item.Status, item.Reference, item.ExpenseCategory, item.Reference, item.MovementDate, item.PaidTo)).ToListAsync(cancellationToken);
        lines.AddRange(siteMovements.Select(item => Resolve(item, AccountingClassificationCode.SiteExpense, "حركة موقع معتمدة كمصروف موقع")));
        var excavations = await db.ExcavationRecords.AsNoTracking().Where(item => item.ProjectId == projectId)
            .Select(item => new RawLine("excavation", item.Id, item.TotalAmount, item.Status, item.Notes, item.AccountCategory, item.Notes, item.ExcavationDate, item.Supplier == null ? null : item.Supplier.Name)).ToListAsync(cancellationToken);
        lines.AddRange(excavations.Select(item => Resolve(item, AccountingClassificationCode.Excavation, "سجل حفر")));

        var concrete = await db.ConcreteRecords.AsNoTracking().Where(item => item.ProjectId == projectId).Select(item => new RawLine("concrete", item.Id, item.TotalAmount, item.Status, item.Notes, item.AccountCategory, item.Notes, item.PourDate, item.Supplier == null ? item.AccountCategory : item.Supplier.Name)).ToListAsync(cancellationToken);
        lines.AddRange(concrete.Select(ClassifyConcrete));

        var steel = await db.SteelRecords.AsNoTracking().Where(item => item.ProjectId == projectId)
            .Select(item => new RawLine("steel", item.Id, item.TotalAmount, item.Status, item.Notes, item.AccountCategory, item.Notes, item.OperationDate, item.Supplier == null ? null : item.Supplier.Name)).ToListAsync(cancellationToken);
        lines.AddRange(steel.Select(item => Resolve(item, AccountingClassificationCode.Steel, "سجل توريد حديد")));

        var finishing = await db.FinishingRecords.AsNoTracking().Where(item => item.ProjectId == projectId).Select(item => new RawLine("finishing", item.Id, item.TotalAmount, item.Status, item.Notes, item.AccountCategory, item.Notes, item.WorkDate, item.Supplier == null ? null : item.Supplier.Name)).ToListAsync(cancellationToken);
        lines.AddRange(finishing.Select(ClassifyFinishing));

        var supplies = await db.SupplierSupplies.AsNoTracking().Where(item => item.ProjectId == projectId)
            .Select(item => new RawLine("supplier-supply", item.Id, item.TotalAmount, item.Status, item.Notes, item.SupplyType, item.Notes, item.SupplyDate, item.Supplier.Name)).ToListAsync(cancellationToken);
        lines.AddRange(supplies.Select(item => Resolve(item, AccountingClassificationCode.SupplierSupply, "توريد مورد منفصل عن تكلفة العمل")));
        var supplierPayments = await db.SupplierPayments.AsNoTracking().Where(item => item.ProjectId == projectId)
            .Select(item => new RawLine("supplier-payment", item.Id, item.Amount, item.Status, item.Notes, null, item.Notes, item.PaymentDate, item.Supplier.Name)).ToListAsync(cancellationToken);
        lines.AddRange(supplierPayments.Select(item => Resolve(item, AccountingClassificationCode.SupplierPayment, "دفعة مورد منفصلة عن أصل التوريد")));

        var advances = await db.AdvancePayments.AsNoTracking().Where(item => item.ProjectId == projectId).Select(item => new RawLine("advance-payment", item.Id, item.Amount, item.Status, item.Notes, null, item.Notes, item.PaymentDate, item.PayerName)).ToListAsync(cancellationToken);
        lines.AddRange(advances.Select(item => Resolve(item, AccountingClassificationCode.OwnerFunding, "إيصال/تمويل من المالك أو الدافع")));
        var advanceIds = advances.Select(item => item.EntityId).ToArray();
        var allocations = await db.AdvanceAllocations.AsNoTracking().Where(item => advanceIds.Contains(item.AdvancePaymentId)).Select(item => new RawLine("advance-allocation", item.Id, item.Amount, item.AdvancePayment.Status, item.Notes, item.AccountCategory, item.Notes, item.AllocationDate, item.AccountCategory)).ToListAsync(cancellationToken);
        lines.AddRange(allocations.Select(item => item.StoredCategory == "المكتب"
            ? Resolve(item, AccountingClassificationCode.OfficePayment, "توزيع دفعة تحت الحساب على المكتب")
            : Resolve(item, AccountingClassificationCode.FundingAllocation, "توزيع دفعة تحت الحساب؛ ليس إيصالًا جديدًا")));

        var totals = AllCodes.Select(code =>
        {
            var matching = lines.Where(item => item.Classification == code).ToList();
            var visible = matching.Where(item => item.IncludedInCurrentTotals).ToList();
            return new AccountingClassificationTotal(code,
                visible.Where(item => item.Provenance.IsImported).Sum(item => item.Amount),
                visible.Sum(item => item.Amount),
                visible.Count(item => item.Provenance.IsImported),
                visible.Count,
                matching.Count(item => item.Resolution == ClassificationResolution.Unresolved));
        }).ToList();

        return new AccountingClassificationPreview(project.Id, project.Code, project.Name, project.OfficeSharePercentage,
            "Lines are read-only; current totals include only معتمد/مرحّل. Imported totals are the imported subset of those same visible rows.",
            totals, lines, lines.Where(item => item.Resolution == ClassificationResolution.Unresolved).ToList());
    }

    private static AccountingClassificationLine ClassifyConcrete(RawLine item)
    {
        var provenance = ParseProvenance(item.ProvenanceText);
        if (provenance.SourceText.Contains("خرسانات مزدوجة D:G:H", StringComparison.OrdinalIgnoreCase))
            return Resolve(item, AccountingClassificationCode.ConcreteStation, "ملف استيراد موثوق: profile D:G:H");
        if (provenance.SourceText.Contains("خرسانات مزدوجة N:R:S:U", StringComparison.OrdinalIgnoreCase))
            return Resolve(item, AccountingClassificationCode.ContractorWorkmanship, "ملف استيراد موثوق: profile N:R:S:U");
        var category = Normalize(item.StoredCategory);
        if (category == "المقاول") return Resolve(item, AccountingClassificationCode.ContractorWorkmanship, "صنف حساب صريح: المقاول");
        if (category is "المحطه" or "المحطة") return Resolve(item, AccountingClassificationCode.ConcreteStation, "صنف حساب صريح: المحطة");
        return Unresolved(item, "لا يوجد دليل موثوق يفصل خرسانة المقاول عن خرسانة المحطة؛ لم يتم التخمين.");
    }

    private static AccountingClassificationLine ClassifyFinishing(RawLine item)
    {
        var sheet = ParseProvenance(item.ProvenanceText).SourceSheet;
        AccountingClassificationCode? bySheet = Normalize(sheet) switch
        {
            "اعمالالمباني" or "حسابالمباني" or "حسابالبنا" or "حسابالطوب" => AccountingClassificationCode.Buildings,
            "اعمالالمحاره" or "المحاره" or "حسابالمحاره" => AccountingClassificationCode.Plaster,
            "اعمالالعزل" => AccountingClassificationCode.Insulation,
            "اعمالالكهرباء" => AccountingClassificationCode.Electrical,
            "اعمالالسباكه" => AccountingClassificationCode.Plumbing,
            _ => null
        };
        if (bySheet is not null) return Resolve(item, bySheet.Value, $"ورقة مصدر موثوقة: {sheet}");
        return Normalize(item.StoredCategory) switch
        {
            "المباني" => Resolve(item, AccountingClassificationCode.Buildings, "صنف حساب صريح: المباني"),
            "المحاره" => Resolve(item, AccountingClassificationCode.Plaster, "صنف حساب صريح: المحارة"),
            "العزل" => Resolve(item, AccountingClassificationCode.Insulation, "صنف حساب صريح: العزل"),
            "الكهرباء" => Resolve(item, AccountingClassificationCode.Electrical, "صنف حساب صريح: الكهرباء"),
            "السباكه" => Resolve(item, AccountingClassificationCode.Plumbing, "صنف حساب صريح: السباكة"),
            _ => Unresolved(item, "التشطيب لا يملك ورقة مصدر أو تصنيفًا صريحًا موثوقًا.")
        };
    }

    private static AccountingClassificationLine Resolve(RawLine item, AccountingClassificationCode code, string evidence) =>
        new(item.EntityType, item.EntityId, code, ClassificationResolution.Resolved, item.Amount, item.Status,
            IsFinanciallyVisible(item.Status), ParseProvenance(item.ProvenanceText, evidence), string.Empty, item.TransactionDate, item.PartyName ?? string.Empty);

    private static AccountingClassificationLine Unresolved(RawLine item, string diagnostic) =>
        new(item.EntityType, item.EntityId, AccountingClassificationCode.Unresolved, ClassificationResolution.Unresolved, item.Amount, item.Status,
            IsFinanciallyVisible(item.Status), ParseProvenance(item.ProvenanceText, "Unresolved"), diagnostic, item.TransactionDate, item.PartyName ?? string.Empty);

    private static ClassificationProvenance ParseProvenance(string? text, string evidence = "")
    {
        var source = text?.Trim() ?? string.Empty;
        var imported = source.StartsWith("استيراد", StringComparison.Ordinal);
        var match = imported ? ImportPattern.Match(source) : Match.Empty;
        if (!match.Success && imported) match = SupplierPaymentImportPattern.Match(source);
        var sheet = match.Success ? match.Groups["sheet"].Value.Trim() : string.Empty;
        int? row = match.Success && int.TryParse(match.Groups["row"].Value, out var parsedRow) ? parsedRow : null;
        return new ClassificationProvenance(imported, source, sheet, row, evidence);
    }

    private static bool IsFinanciallyVisible(string status) => status is "معتمد" or "مرحّل";

    private static string Normalize(string? value) => string.Join(string.Empty, (value ?? string.Empty).Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .Replace("ة", "ه").Replace("ى", "ي").Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا");

    private sealed record RawLine(string EntityType, int EntityId, decimal Amount, string Status, string? ProvenanceText, string? StoredCategory, string? SourceText, DateTime? TransactionDate, string? PartyName);
}
