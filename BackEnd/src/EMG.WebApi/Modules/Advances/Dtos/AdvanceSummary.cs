namespace EMG.Api.Modules.Advances.Dtos;

public sealed record AdvanceSummary(
    int Id,
    int ProjectId,
    string ProjectName,
    string ClientName,
    string PayerName,
    decimal Amount,
    decimal AllocatedAmount,
    decimal RemainingAmount,
    decimal UnallocatedAmount,
    decimal DueFromPayer,
    string BalanceStatus,
    DateTime PaymentDate,
    string PaymentMethod,
    string Reference,
    string Status,
    string Notes);
