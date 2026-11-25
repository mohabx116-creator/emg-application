namespace EMG.Domain.Entities;

public sealed class ContractorStatement
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int ContractorId { get; set; }
    public Contractor Contractor { get; set; } = null!;
    public int ContractId { get; set; }
    public ContractorContract Contract { get; set; } = null!;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public string Currency { get; set; } = "EGP";
    public string Status { get; set; } = "مسودة";
    public decimal GrossAmount { get; set; }
    public decimal PreviousPaymentsAmount { get; set; }
    public decimal DeductionsAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string? SourceReference { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ContractorStatementLine> Lines { get; set; } = [];
}
