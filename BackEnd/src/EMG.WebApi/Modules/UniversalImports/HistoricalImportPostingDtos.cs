namespace EMG.Api.Modules.UniversalImports;

public static class HistoricalImportStatuses
{
    public const string PreviewOnly = "PREVIEW_ONLY";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string ReadyForApproval = "READY_FOR_APPROVAL";
    public const string ApprovedForPosting = "APPROVED_FOR_POSTING";
    public const string Posted = "POSTED";
    public const string Rejected = "REJECTED";
    internal const string UniversalProjectCommitted = "UNIVERSAL_NEW_PROJECT_COMMITTED";
}

public sealed record HistoricalImportStageRequest(
    int ProjectId,
    string SourceApprovalReference,
    UniversalPreviewResponse Preview);

public sealed record HistoricalImportBatchReport(
    int BatchId,
    int ProjectId,
    string SourceSha256,
    string Status,
    int TotalRecords,
    int ReadyRecords,
    int ApprovedRecords,
    int PostedRecords,
    int ReviewRequiredRecords,
    int ExcludedRecords,
    IReadOnlyList<HistoricalImportStreamReport> Streams,
    IReadOnlyList<HistoricalImportRecordReport> Records);

public sealed record HistoricalImportStreamReport(
    string StreamKey,
    string Currency,
    decimal Amount,
    int RecordCount,
    int PostedCount);

public sealed record HistoricalImportRecordReport(
    int RecordId,
    string DeterministicIdentity,
    string SheetName,
    string SourceCoordinate,
    string MappingFamily,
    string BusinessEventType,
    decimal? Amount,
    string Currency,
    string SemanticValidationState,
    string LifecycleStatus,
    bool IsDuplicateCandidate,
    bool EligibleForPosting,
    string? ExclusionReason);
