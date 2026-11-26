namespace EMG.Domain.Entities;

public sealed class ContractorPayment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int ContractorId { get; set; }
    public Contractor Contractor { get; set; } = null!;
    public int? ContractorStatementId { get; set; }
    public ContractorStatement? ContractorStatement { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;
    public string PaymentMethod { get; set; } = "نقدي";
    public string PaymentSource { get; set; } = "مكتب";
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
