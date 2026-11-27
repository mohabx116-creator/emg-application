namespace EMG.Api.Modules.Advances.Dtos;

public sealed record CreateAdvanceRequest(
    int ProjectId,
    string? ClientName,
    string? PayerName,
    decimal Amount,
    DateTime? PaymentDate,
    string? PaymentMethod,
    string? Reference,
    string? Notes,
    string? Currency = null);
