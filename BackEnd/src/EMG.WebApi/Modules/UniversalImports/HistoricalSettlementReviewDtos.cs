namespace EMG.Api.Modules.UniversalImports;

public sealed record HistoricalSettlementReview(
    int BatchId,
    int ProjectId,
    string SourceFileName,
    string SourceSha256,
    string BatchStatus,
    int Version,
    IReadOnlyList<HistoricalSettlementRecord> Records);

public sealed record HistoricalSettlementRecord(
    int RecordId,
    string SheetName,
    string SourceCoordinate,
    IReadOnlyList<string> SourceCells,
    string MappingFamily,
    string Stream,
    string BusinessEventType,
    decimal? Amount,
    string Currency,
    string? Target,
    string? Party,
    string? Reference,
    string SemanticValidationState,
    string LifecycleStatus,
    bool IsReferenceOnly,
    bool IsDuplicateCandidate,
    string? ReviewReason,
    string? LinkedObligationType,
    int? LinkedObligationId,
    decimal? LinkedObligationAmount,
    int? OperationalPaymentId = null,
    string? OperationalPaymentStatus = null);

public sealed record HistoricalSettlementDecision(
    int Version,
    int RecordId,
    string Decision,
    string? Evidence,
    string? ObligationType = null,
    int? ObligationId = null,
    decimal? ObligationAmount = null,
    string? Party = null);

public sealed record HistoricalSettlementLink(
    string ObligationType,
    int ObligationId,
    decimal ObligationAmount,
    string Party,
    string Evidence,
    int? OperationalPaymentId = null,
    string? OperationalPaymentStatus = null);
