namespace EMG.Domain.Entities;

public sealed class SupplierInvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public SupplierInvoice Invoice { get; set; } = null!;
    public int SupplierSupplyId { get; set; }
    public SupplierSupply SupplierSupply { get; set; } = null!;
    public int LineNumber { get; set; }
}
