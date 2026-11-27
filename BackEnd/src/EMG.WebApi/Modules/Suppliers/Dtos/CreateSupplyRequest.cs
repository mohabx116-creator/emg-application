namespace EMG.Api.Modules.Suppliers.Dtos;

public sealed record CreateSupplyRequest(
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
