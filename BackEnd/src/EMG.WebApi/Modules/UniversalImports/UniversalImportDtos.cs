namespace EMG.Api.Modules.UniversalImports;

public enum UniversalRecordType
{
    Transaction,
    Funding,
    Payment,
    Expense,
    Liability,
    Allocation,
    OpeningBalance,
    Adjustment,
    DerivedValue,
    Summary,
    Unresolved
}

public sealed record UniversalMappingDefinition(
    string FamilyKey,
    string Version,
    string Name,
    IReadOnlyList<string> Evidence,
    bool SupportsPosting);

public sealed record UniversalPreviewResponse(
    string FileName,
    long FileSize,
    string SourceSha256,
    string EngineVersion,
    bool RequiresMapping,
    IReadOnlyList<UniversalSectionPreview> Sections,
    IReadOnlyList<UniversalRecordPreview> Records,
    IReadOnlyList<UniversalStreamTotal> Totals,
    IReadOnlyList<UniversalReconciliationPreview> Reconciliations,
    IReadOnlyList<UniversalDuplicateCandidate> DuplicateCandidates,
    IReadOnlyList<string> Exceptions,
    string? SourceProjectCode = null);

public sealed record UniversalSectionPreview(
    string SectionId,
    string SheetName,
    string SourceRange,
    string StreamKey,
    string MappingFamily,
    string MappingVersion,
    string Confidence,
    bool RequiresMapping,
    bool EligibleForPosting,
    int SourceRowCount,
    int ExtractedRecordCount,
    IReadOnlyList<string> Exceptions);

public sealed record UniversalRecordPreview(
    string RecordId,
    string SectionId,
    string SheetName,
    string SourceCoordinate,
    IReadOnlyList<string> SourceCells,
    UniversalRecordType RecordType,
    string StreamKey,
    string Currency,
    decimal? Amount,
    bool IsFormulaDerived,
    bool IsSummary,
    bool EligibleForPosting,
    string Confidence,
    string ValueFingerprint,
    UniversalOperationalEvidence? OperationalEvidence = null);

public sealed record UniversalOperationalEvidence(
    string Target, DateOnly Date, string Description, decimal? Quantity, string? Unit, decimal? UnitPrice,
    string? Party, string? Reference, string AmountCell, bool RequiresManualMapping = false);

public sealed record UniversalStreamTotal(
    string StreamKey,
    string Currency,
    decimal Amount,
    int RecordCount,
    int EligibleRecordCount);

public sealed record UniversalReconciliationPreview(
    string SectionId,
    string StreamKey,
    string Currency,
    decimal PreviewAmount,
    decimal? SourceSummaryAmount,
    decimal? Difference,
    string Status,
    string EvidenceCoordinate);

public sealed record UniversalDuplicateCandidate(
    string ValueFingerprint,
    IReadOnlyList<string> RecordIds,
    int Count);
