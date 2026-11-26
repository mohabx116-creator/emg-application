namespace EMG.Api.Modules.Contractors.Dtos;

public sealed record CreateContractorPaymentRequest(
    int ProjectId,
    int? ContractorStatementId,
    decimal Amount,
    string? Currency,
    DateTime? PaymentDate,
    string? PaymentMethod,
    string? Reference,
    string? Notes,
    string? PaymentSource);

public sealed record ContractorPaymentSummary(
    int Id,
    int ProjectId,
    int ContractorId,
    int? ContractorStatementId,
    decimal Amount,
    string Currency,
    DateTime PaymentDate,
    string PaymentMethod,
    string PaymentSource,
    string Reference,
    string Notes);
