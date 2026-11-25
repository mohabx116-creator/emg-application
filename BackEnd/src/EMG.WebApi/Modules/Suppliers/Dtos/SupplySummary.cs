namespace EMG.Api.Modules.Suppliers.Dtos;

public sealed record SupplySummary(
    int Id,
    int SupplierId,
    string SupplierName,
    int ProjectId,
    string ProjectName,
    string SupplyType,
    string ItemName,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    DateTime SupplyDate,
    string InvoiceNumber,
    string Notes,
    string Status);
