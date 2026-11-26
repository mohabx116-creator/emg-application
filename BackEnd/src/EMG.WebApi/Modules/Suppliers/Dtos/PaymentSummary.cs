namespace EMG.Api.Modules.Suppliers.Dtos;

public sealed record PaymentSummary(
    int Id,
    int SupplierId,
    int? ProjectId,
    string ProjectName,
    int? SupplyId,
    decimal Amount,
    DateTime PaymentDate,
    string PaymentMethod,
    string Reference,
    string Notes,
    string Status,
    string PaymentSource = "مكتب");
