namespace EMG.Api.Modules.ReportBuilder.Dtos;

public sealed record ReportBuilderQuery(
    IReadOnlyList<int>? ProjectIds = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<string>? Parties = null,
    IReadOnlyList<string>? Dimensions = null,
    string DetailLevel = "both",
    IReadOnlyList<string>? GroupBy = null,
    IReadOnlyList<string>? Columns = null,
    string Snapshot = "both",
    string SortBy = "project",
    bool IncludeUnresolved = true);

public sealed record ReportBuilderPreview(
    DateTime GeneratedAtUtc,
    ReportBuilderQuery Query,
    IReadOnlyList<string> VisibleColumns,
    IReadOnlyList<ReportBuilderSummaryRow> Summary,
    IReadOnlyList<ReportBuilderTransactionRow> Transactions,
    IReadOnlyList<string> Warnings,
    bool IsComplete)
{
    internal IReadOnlyList<ReportBuilderProjectAmounts> Projects { get; init; } = [];
}

public sealed record ReportBuilderWorkbookRequest(ReportBuilderQuery Query, string Presentation = "custom");

internal sealed record ReportBuilderAmounts(decimal Imported, decimal Current);

internal sealed record ReportBuilderProjectAmounts(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    IReadOnlyDictionary<string, ReportBuilderAmounts> ByCategory);

public sealed record ReportBuilderSummaryRow(
    string GroupKey,
    string GroupLabel,
    decimal ImportedAmount,
    decimal CurrentAmount,
    decimal UnresolvedAmount,
    int RecordCount,
    int UnresolvedCount,
    bool IsComplete);

public sealed record ReportBuilderTransactionRow(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    string EntityType,
    int EntityId,
    string Category,
    string Dimension,
    string Party,
    DateTime? TransactionDate,
    decimal ImportedAmount,
    decimal CurrentAmount,
    decimal UnresolvedAmount,
    string Status,
    string SourceText,
    string SourceSheet,
    int? SourceRow,
    bool IsUnresolved,
    string Diagnostic)
{
    internal bool IncludedInFinancialTotals { get; init; }
}
