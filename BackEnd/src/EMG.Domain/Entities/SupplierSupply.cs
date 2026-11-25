namespace EMG.Domain.Entities;

public sealed class SupplierSupply
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string SupplyType { get; set; } = "خامات";
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime SupplyDate { get; set; } = DateTime.UtcNow.Date;
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "مسودة";
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<SupplierPayment> Payments { get; set; } = [];
}
