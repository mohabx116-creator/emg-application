namespace EMG.Domain.Entities;

public sealed class SupplierPayment
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? SupplyId { get; set; }
    public SupplierSupply? Supply { get; set; }
    public int? InvoiceId { get; set; }
    public SupplierInvoice? Invoice { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;
    public string PaymentMethod { get; set; } = "نقدي";
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "مسودة";
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
