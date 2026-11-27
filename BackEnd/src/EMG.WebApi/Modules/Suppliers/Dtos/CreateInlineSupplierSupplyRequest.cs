namespace EMG.Api.Modules.Suppliers.Dtos;

public sealed record CreateInlineSupplierSupplyRequest(
    string SupplierName,
    string? SupplierType,
    string? Phone,
    string? TaxNumber,
    string? SupplierNotes,
    int ProjectId,
    string SupplyType,
    string ItemName,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    DateTime? SupplyDate,
    string? InvoiceNumber,
    string? Notes,
    [EgpOnly] string? Currency = null);

public sealed record InlineSupplierSupplySummary(SupplierSummary Supplier, SupplySummary Supply);
