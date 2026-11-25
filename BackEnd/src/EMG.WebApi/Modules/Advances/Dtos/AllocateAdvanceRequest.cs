namespace EMG.Api.Modules.Advances.Dtos;

public sealed record AllocateAdvanceRequest(
    int? SiteMovementId,
    int? SupplierPaymentId,
    decimal Amount,
    DateTime? AllocationDate,
    string? Notes,
    string? AccountCategory = null);
