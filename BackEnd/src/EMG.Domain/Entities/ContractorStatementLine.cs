namespace EMG.Domain.Entities;

public sealed class ContractorStatementLine
{
    public int Id { get; set; }
    public int StatementId { get; set; }
    public ContractorStatement Statement { get; set; } = null!;
    public int ContractItemId { get; set; }
    public ContractorContractItem ContractItem { get; set; } = null!;
    public string SourceEntityType { get; set; } = string.Empty;
    public int SourceEntityId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitRate { get; set; }
    public decimal GrossAmount { get; set; }
    public string SourceReference { get; set; } = string.Empty;
}
