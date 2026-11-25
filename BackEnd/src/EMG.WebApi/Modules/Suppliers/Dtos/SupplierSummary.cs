namespace EMG.Api.Modules.Suppliers.Dtos;

public sealed record SupplierSummary(
    int Id,
    string Name,
    string SupplierType,
    string Phone,
    string TaxNumber,
    string Notes,
    bool IsActive,
    int SuppliesCount,
    decimal TotalSupplies,
    decimal TotalPaid,
    decimal Remaining);
