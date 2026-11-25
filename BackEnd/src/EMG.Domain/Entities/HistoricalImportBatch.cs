namespace EMG.Domain.Entities;

public sealed class HistoricalImportBatch
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string SourceSha256 { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string MappingVersion { get; set; } = string.Empty;
    public string Status { get; set; } = "PREVIEW_ONLY";
    public string? SourceApprovalReference { get; set; }
    public int CreatedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public int Version { get; set; }
    public ICollection<HistoricalImportRecord> Records { get; set; } = [];
    public ICollection<HistoricalImportAuditEvent> AuditEvents { get; set; } = [];
}
