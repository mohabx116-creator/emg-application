namespace EMG.Domain.Entities;

public sealed class HistoricalWorkLine
{
    public long Id { get; set; }
    public int BatchId { get; set; }
    public HistoricalImportBatch Batch { get; set; } = null!;
    public string Sheet { get; set; } = string.Empty;
    public string Cell { get; set; } = string.Empty;
    public int SourceRow { get; set; }
    public string Family { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Basis { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime? WorkDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal? Progress { get; set; }
    public string Party { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public int? ContractorId { get; set; }
    public string SourceJson { get; set; } = string.Empty;
    public string? OperationalTable { get; set; }
    public int? OperationalId { get; set; }
}
