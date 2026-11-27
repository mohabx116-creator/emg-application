namespace EMG.Domain.Entities;

public sealed class HistoricalSettlement
{
    public long Id { get; set; }
    public int BatchId { get; set; }
    public HistoricalImportBatch Batch { get; set; } = null!;
    public string Sheet { get; set; } = string.Empty;
    public string Cell { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime? SettlementDate { get; set; }
    public string Party { get; set; } = string.Empty;
    public string Narrative { get; set; } = string.Empty;
    public string SourceJson { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public int? ContractorId { get; set; }
}
