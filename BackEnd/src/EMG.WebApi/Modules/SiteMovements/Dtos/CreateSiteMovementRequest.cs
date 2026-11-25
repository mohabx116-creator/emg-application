namespace EMG.Api.Modules.SiteMovements.Dtos;

public sealed record CreateSiteMovementRequest(
    int ProjectId,
    int? SupplierId,
    string MovementType,
    string Description,
    string? Unit,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? Amount,
    DateTime? MovementDate,
    string? Reference,
    string? Details,
    string? ExpenseCategory,
    string? PaidTo,
    string? PaymentMethod,
    bool? IsPaid);
