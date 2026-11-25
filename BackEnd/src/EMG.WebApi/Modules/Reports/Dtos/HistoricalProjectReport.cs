namespace EMG.Api.Modules.Reports.Dtos;

public sealed record HistoricalProjectReport(
    int ProjectId,
    IReadOnlyList<HistoricalReportTotal> Totals,
    IReadOnlyList<HistoricalReportEntry> Entries);

public sealed record HistoricalReportTotal(
    string LedgerStream,
    string Currency,
    decimal Amount,
    int EntryCount);

public sealed record HistoricalReportEntry(
    int Id,
    int BatchId,
    string SourceFileName,
    string SourceSha256,
    string SheetName,
    string SourceCoordinate,
    string MappingFamily,
    string BusinessEventType,
    string LedgerStream,
    decimal Amount,
    string Currency,
    DateTime PostedAtUtc,
    string ProvenanceJson);
