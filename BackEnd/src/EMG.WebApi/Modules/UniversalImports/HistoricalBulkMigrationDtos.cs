namespace EMG.Api.Modules.UniversalImports;

public sealed record HistoricalBulkMigrationProjectResult(
    string FileName,
    string SourceSha256,
    string? SourceProjectCode,
    int? ProjectId,
    int? BatchId,
    string Status,
    int PreviewRecordCount,
    int OperationalRecordCount,
    int PreservedRecordCount,
    IReadOnlyDictionary<string, decimal> CurrencyTotals,
    IReadOnlyList<string> Exceptions);

public sealed record HistoricalBulkMigrationResult(
    string SourceRoot,
    DateTime CutoffUtc,
    int CandidateWorkbookCount,
    int MigratedProjectCount,
    int PreservedRecordCount,
    IReadOnlyList<HistoricalBulkMigrationProjectResult> Projects,
    IReadOnlyList<string> SkippedFiles);
