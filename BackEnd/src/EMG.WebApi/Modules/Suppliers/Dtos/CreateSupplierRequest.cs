namespace EMG.Api.Modules.Suppliers.Dtos;

public sealed record CreateSupplierRequest(
    string Name,
    string? SupplierType,
    string? Phone,
    string? TaxNumber,
    string? Notes);
