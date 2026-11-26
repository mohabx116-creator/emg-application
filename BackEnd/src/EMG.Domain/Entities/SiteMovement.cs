namespace EMG.Domain.Entities;

public sealed class SiteMovement
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int? ContractorId { get; set; }
    public Contractor? Contractor { get; set; }
    public string CounterpartyType { get; set; } = "بدون طرف";
    public string Currency { get; set; } = "EGP";
    public string? FundingSource { get; set; }
    public string? Recipient { get; set; }
    public string? WorkCategory { get; set; }
    public string MovementType { get; set; } = "مصروفات";
    public string Description { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ExpenseCategory { get; set; }
    public string? PaidTo { get; set; }
    public string? PaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.UtcNow.Date;
    public string Status { get; set; } = "مسودة";
    public string? Reference { get; set; }
    public string? Details { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
