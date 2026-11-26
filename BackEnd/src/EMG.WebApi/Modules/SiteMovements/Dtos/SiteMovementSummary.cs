namespace EMG.Api.Modules.SiteMovements.Dtos;

public sealed record SiteMovementSummary(
    int Id,
    int ProjectId,
    string ProjectName,
    int? SupplierId,
    string SupplierName,
    string MovementType,
    string Description,
    string Unit,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal TotalAmount,
    DateTime MovementDate,
    string Status,
    string Reference,
    string Details,
    int? CreatedByUserId,
    string ExpenseCategory,
    string PaidTo,
    string PaymentMethod,
    bool IsPaid,
    int? InvoiceId);
