namespace EMG.Api.Modules.Suppliers.Dtos;

public sealed record SupplierInvoiceLineRequest(string Description, string Unit, decimal Quantity, decimal UnitPrice, string? SupplyType);

public sealed record CreateSupplierInvoiceRequest(
    int SupplierId,
    int ProjectId,
    int? SiteMovementId,
    DateTime? InvoiceDate,
    IReadOnlyList<SupplierInvoiceLineRequest> Lines,
    decimal InitialPayment,
    string? PaymentMethod,
    string? PaymentReference,
    string? Notes);

public sealed record UpdateSupplierInvoiceRequest(IReadOnlyList<SupplierInvoiceLineRequest> Lines, DateTime? InvoiceDate, string? Notes);

public sealed record SupplierInvoiceLineSummary(int Id, int SupplyId, string Description, string Unit, decimal Quantity, decimal UnitPrice, decimal TotalAmount);

public sealed record SupplierInvoiceSummary(
    int Id, int SupplierId, int ProjectId, int? SiteMovementId, string InvoiceNumber, string Currency,
    decimal TotalAmount, decimal PaidAmount, decimal OutstandingAmount, IReadOnlyList<SupplierInvoiceLineSummary> Lines);
