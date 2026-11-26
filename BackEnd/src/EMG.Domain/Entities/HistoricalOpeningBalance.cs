namespace EMG.Domain.Entities;

public sealed class HistoricalOpeningBalance
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int BatchId { get; set; }
    public HistoricalImportBatch Batch { get; set; } = null!;
    public string StreamKey { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
    public decimal Amount { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string ProvenanceJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
