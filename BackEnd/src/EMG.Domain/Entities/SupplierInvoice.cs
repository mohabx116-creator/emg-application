namespace EMG.Domain.Entities;

public sealed class SupplierInvoice
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int? SiteMovementId { get; set; }
    public SiteMovement? SiteMovement { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
    public string Status { get; set; } = "مرحّل";
    public int? CreatedByUserId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<SupplierSupply> Supplies { get; set; } = [];
    public ICollection<SupplierInvoiceLine> Lines { get; set; } = [];
    public ICollection<SupplierPayment> Payments { get; set; } = [];
}
