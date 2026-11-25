namespace EMG.Domain.Entities;

public sealed class ContractorContractItem
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public ContractorContract Contract { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal ApprovedQuantity { get; set; }
    public decimal UnitRate { get; set; }
    public string? SourceReference { get; set; }
    public bool IsActive { get; set; } = true;
}
