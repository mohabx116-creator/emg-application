using EMG.Api.Modules.AccountingClassification.Dtos;
using EMG.Api.Modules.AccountingClassification.Services;
using EMG.Api.Modules.ReportBuilder.Dtos;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.ReportBuilder.Services;

public sealed class ReportBuilderService(EmgDbContext db, IAccountingClassificationService classificationService) : IReportBuilderService
{
    public async Task<ReportBuilderPreview> PreviewAsync(ReportBuilderQuery query, int? userId, bool isGlobalAccess, CancellationToken cancellationToken, bool includeProjectAmounts = false)
    {
        var projectsQuery = db.Projects.AsNoTracking();
        if (!isGlobalAccess)
        {
            if (userId is null) return Empty(query, "لا توجد صلاحية فعالة لعرض المشاريع.");
            projectsQuery = projectsQuery.Where(project => db.UserProjectAssignments.Any(assignment => assignment.ProjectId == project.Id && assignment.UserId == userId && assignment.IsActive));
        }
        if (query.ProjectIds is { Count: > 0 }) projectsQuery = projectsQuery.Where(project => query.ProjectIds.Contains(project.Id));
        var projects = await projectsQuery.OrderBy(project => project.Code).ThenBy(project => project.Id).Select(project => new ProjectRef(project.Id)).ToListAsync(cancellationToken);

        var lines = new List<BuilderLine>();
        var recoveredProjects = new HashSet<int>();
        foreach (var project in projects)
        {
            var preview = await classificationService.PreviewAsync(project.Id, userId, isGlobalAccess, cancellationToken);
            if (preview is null) continue;
            if (preview.HasRecoveredLedger) recoveredProjects.Add(project.Id);
            lines.AddRange(preview.Lines.Select(line => new BuilderLine(preview.ProjectId, preview.ProjectCode, preview.ProjectName, line)));
        }

        var warnings = new List<string>
        {
            "مبالغ منشئ التقارير بالجنيه المصري فقط. يعرض الحساب الختامي للمشروع الأرصدة بحسب العملة.",
            "H يظل إجماليًا تشخيصيًا وقد يتداخل مع J/K؛ لا يوجد إجمالي مالي شامل في هذه المعاينة.",
            "T غير معتمد ولا يتم حسابه أو عرضه كرصيد رسمي.",
            "استحقاق المكتب والدفعات والتوزيعات أبعاد منفصلة وليست جزءًا من إجمالي واحد."
        };
        if (query.IncludeUnresolved == false) warnings.Add("تم استبعاد السجلات غير المحسومة بناءً على اختيار المستخدم؛ راجعها قبل اعتماد أي نتيجة.");

        var filtered = lines.Where(line => Matches(line, query)).ToList();
        var visible = filtered.Where(line => query.IncludeUnresolved || !line.Line.Resolution.Equals(ClassificationResolution.Unresolved)).ToList();
        var summary = BuildSummary(visible, query);
        var transactions = query.DetailLevel.Equals("totals", StringComparison.OrdinalIgnoreCase) ? [] : BuildTransactions(visible, query);
        if (query.DetailLevel.Equals("transactions", StringComparison.OrdinalIgnoreCase)) summary = [];
        var complete = filtered.All(line => line.Line.Resolution != ClassificationResolution.Unresolved) && query.IncludeUnresolved;
        return new ReportBuilderPreview(DateTime.UtcNow, query, query.Columns?.Count > 0 ? query.Columns : DefaultColumns,
            summary, transactions, warnings, complete)
        {
            Projects = includeProjectAmounts ? BuildProjectAmounts(visible).Select(p => p with { HasRecoveredLedger = recoveredProjects.Contains(p.ProjectId) }).ToList() : []
        };
    }

    private static List<ReportBuilderProjectAmounts> BuildProjectAmounts(List<BuilderLine> lines) => lines
        .GroupBy(row => new { row.ProjectId, row.ProjectCode, row.ProjectName })
        .Select(project => new ReportBuilderProjectAmounts(project.Key.ProjectId, project.Key.ProjectCode, project.Key.ProjectName,
            project.GroupBy(row => row.Line.Classification.ToString()).ToDictionary(category => category.Key, category => Measure(category))))
        .ToList();

    private static ReportBuilderAmounts Measure(IEnumerable<BuilderLine> rows)
    {
        decimal imported = 0, current = 0;
        foreach (var row in rows.Where(row => row.Line.IncludedInCurrentTotals))
        {
            current += row.Line.Amount;
            if (row.Line.Provenance.IsImported) imported += row.Line.Amount;
        }
        return new ReportBuilderAmounts(imported, current);
    }

    private static List<ReportBuilderSummaryRow> BuildSummary(List<BuilderLine> lines, ReportBuilderQuery query)
    {
        var groups = query.GroupBy is { Count: > 0 } ? query.GroupBy : ["project"];
        return lines.GroupBy(line => GroupKey(line, groups)).Select(group =>
        {
            var rows = group.ToList();
            var unresolved = rows.Where(row => row.Line.Resolution == ClassificationResolution.Unresolved).ToList();
            var amounts = Measure(rows);
            return new ReportBuilderSummaryRow(group.Key, GroupLabel(rows, groups), amounts.Imported, amounts.Current,
                unresolved.Where(row => row.Line.IncludedInCurrentTotals).Sum(row => row.Line.Amount), rows.Count, unresolved.Count, unresolved.Count == 0);
        }).OrderBy(row => row.GroupKey, StringComparer.Ordinal).ToList();
    }

    private static List<ReportBuilderTransactionRow> BuildTransactions(List<BuilderLine> lines, ReportBuilderQuery query) =>
        Sort(lines.Select(ToTransaction).ToList(), query.SortBy);

    private static ReportBuilderTransactionRow ToTransaction(BuilderLine item)
    {
        var line = item.Line;
        return new ReportBuilderTransactionRow(item.ProjectId, item.ProjectCode, item.ProjectName, line.EntityType, line.EntityId,
            line.Classification.ToString(), Dimension(line.Classification), line.PartyName ?? string.Empty, line.TransactionDate,
            line.IncludedInCurrentTotals && line.Provenance.IsImported ? line.Amount : 0m,
            line.IncludedInCurrentTotals ? line.Amount : 0m,
            line.IncludedInCurrentTotals && line.Resolution == ClassificationResolution.Unresolved ? line.Amount : 0m,
            line.Status, line.Provenance.SourceText, line.Provenance.SourceSheet, line.Provenance.SourceRow,
            line.Resolution == ClassificationResolution.Unresolved, line.Diagnostic)
        {
            IncludedInFinancialTotals = line.IncludedInCurrentTotals
        };
    }

    private static List<ReportBuilderTransactionRow> Sort(List<ReportBuilderTransactionRow> rows, string sortBy) => sortBy.ToLowerInvariant() switch
    {
        "date" => rows.OrderBy(row => row.TransactionDate).ThenBy(row => row.ProjectCode).ThenBy(row => row.EntityId).ToList(),
        "amount" => rows.OrderByDescending(row => row.CurrentAmount).ThenBy(row => row.ProjectCode).ThenBy(row => row.EntityId).ToList(),
        "category" => rows.OrderBy(row => row.Category).ThenBy(row => row.ProjectCode).ThenBy(row => row.EntityId).ToList(),
        "party" => rows.OrderBy(row => row.Party).ThenBy(row => row.ProjectCode).ThenBy(row => row.EntityId).ToList(),
        _ => rows.OrderBy(row => row.ProjectCode).ThenBy(row => row.ProjectName).ThenBy(row => row.EntityId).ToList()
    };

    private static bool Matches(BuilderLine item, ReportBuilderQuery query)
    {
        var line = item.Line;
        if (query.Categories is { Count: > 0 } && !query.Categories.Contains(line.Classification.ToString(), StringComparer.OrdinalIgnoreCase)) return false;
        if (query.Parties is { Count: > 0 } && !query.Parties.Contains(line.PartyName ?? string.Empty, StringComparer.OrdinalIgnoreCase)) return false;
        if (query.Dimensions is { Count: > 0 } && !query.Dimensions.Contains(Dimension(line.Classification), StringComparer.OrdinalIgnoreCase)) return false;
        if (query.FromDate is not null && (line.TransactionDate is null || line.TransactionDate.Value.Date < query.FromDate.Value.Date)) return false;
        if (query.ToDate is not null && (line.TransactionDate is null || line.TransactionDate.Value.Date > query.ToDate.Value.Date)) return false;
        return query.Snapshot.ToLowerInvariant() switch
        {
            "imported" => line.IncludedInCurrentTotals && line.Provenance.IsImported,
            "current" => line.IncludedInCurrentTotals,
            _ => true
        };
    }

    private static string GroupKey(BuilderLine item, IReadOnlyList<string> groups) => string.Join(" | ", groups.Select(group => GroupValue(item, group)));
    private static string GroupLabel(IReadOnlyList<BuilderLine> rows, IReadOnlyList<string> groups) => string.Join(" · ", groups.Select(group => GroupValue(rows[0], group)));
    private static string GroupValue(BuilderLine item, string group) => group.ToLowerInvariant() switch
    {
        "category" => item.Line.Classification.ToString(),
        "party" => item.Line.PartyName ?? "غير محدد",
        "date" => item.Line.TransactionDate?.ToString("yyyy-MM-dd") ?? "بدون تاريخ",
        "dimension" => Dimension(item.Line.Classification),
        _ => $"{item.ProjectCode} · {item.ProjectName}"
    };

    private static string Dimension(AccountingClassificationCode code) => code switch
    {
        AccountingClassificationCode.OwnerFunding => "owner-funding",
        AccountingClassificationCode.FundingAllocation => "advance-allocation",
        AccountingClassificationCode.OfficePayment => "office-payment",
        AccountingClassificationCode.SupplierSupply => "supplier-supply",
        AccountingClassificationCode.SupplierPayment => "supplier-payment",
        AccountingClassificationCode.Unresolved => "unresolved",
        _ => "operational-cost"
    };

    private static ReportBuilderPreview Empty(ReportBuilderQuery query, string warning) => new(DateTime.UtcNow, query, query.Columns ?? DefaultColumns, [], [], [warning], false);
    private static readonly IReadOnlyList<string> DefaultColumns = ["project", "category", "party", "date", "importedAmount", "currentAmount", "unresolvedAmount", "source"];
    private sealed record ProjectRef(int Id);
    private sealed record BuilderLine(int ProjectId, string ProjectCode, string ProjectName, AccountingClassificationLine Line);
}
