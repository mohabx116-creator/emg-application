namespace EMG.Domain.Entities;

public sealed class HistoricalImportRecord
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public HistoricalImportBatch Batch { get; set; } = null!;
    public string DeterministicIdentity { get; set; } = string.Empty;
    public string SectionId { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public string SourceCoordinate { get; set; } = string.Empty;
    public string SourceCellsJson { get; set; } = "[]";
    public string MappingFamily { get; set; } = string.Empty;
    public string MappingVersion { get; set; } = string.Empty;
    public string BusinessEventType { get; set; } = string.Empty;
    public decimal? OriginalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string SemanticValidationState { get; set; } = "REVIEW_REQUIRED";
    public string LifecycleStatus { get; set; } = "REVIEW_REQUIRED";
    public bool IsSummary { get; set; }
    public bool IsDuplicateCandidate { get; set; }
    public bool EligibleForPosting { get; set; }
    public string ValueFingerprint { get; set; } = string.Empty;
    public HistoricalPostedEntry? PostedEntry { get; set; }
    public string? ExclusionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
