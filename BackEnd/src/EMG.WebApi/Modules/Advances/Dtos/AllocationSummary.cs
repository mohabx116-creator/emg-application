namespace EMG.Api.Modules.Advances.Dtos;

public sealed record AllocationSummary(
    int Id,
    int AdvancePaymentId,
    int? SiteMovementId,
    int? SupplierPaymentId,
    string AccountCategory,
    decimal Amount,
    DateTime AllocationDate,
    string Notes);
