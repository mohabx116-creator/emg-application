namespace EMG.Domain.Entities;

public sealed class HistoricalPostedEntry
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int BatchId { get; set; }
    public HistoricalImportBatch Batch { get; set; } = null!;
    public int HistoricalRecordId { get; set; }
    public HistoricalImportRecord HistoricalRecord { get; set; } = null!;
    public string DeterministicIdentity { get; set; } = string.Empty;
    public string LedgerStream { get; set; } = string.Empty;
    public string BusinessEventType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string SourceSha256 { get; set; } = string.Empty;
    public string ProvenanceJson { get; set; } = "{}";
    public int PostedByUserId { get; set; }
    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
}
