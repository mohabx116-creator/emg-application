namespace EMG.Api.Modules.Suppliers.Dtos;

public sealed record CreateSupplierPaymentRequest(
    int? ProjectId,
    int? SupplyId,
    decimal Amount,
    DateTime? PaymentDate,
    string? PaymentMethod,
    string? Reference,
    string? Notes,
    string? PaymentSource = null, string? Currency = null);
