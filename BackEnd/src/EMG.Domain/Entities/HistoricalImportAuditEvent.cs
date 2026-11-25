namespace EMG.Domain.Entities;

public sealed class HistoricalImportAuditEvent
{
    public long Id { get; set; }
    public int BatchId { get; set; }
    public HistoricalImportBatch Batch { get; set; } = null!;
    public int? RecordId { get; set; }
    public HistoricalImportRecord? Record { get; set; }
    public string Action { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
