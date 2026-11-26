namespace EMG.Domain.Entities;

// An original funding schedule can identify a project and a trade without
// identifying an individual receipt. Such allocations have no receipt FK.
public sealed class HistoricalFundingLine
{
    public long Id { get; set; }
    public int BatchId { get; set; }
    public HistoricalImportBatch Batch { get; set; } = null!;
    public string Sheet { get; set; } = string.Empty;
    public string Cell { get; set; } = string.Empty;
    public int SourceRow { get; set; }
    public string Stream { get; set; } = string.Empty;
    public string Basis { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? SourceDate { get; set; }
    public string Narrative { get; set; } = string.Empty;
    public string SourceJson { get; set; } = string.Empty;
}
