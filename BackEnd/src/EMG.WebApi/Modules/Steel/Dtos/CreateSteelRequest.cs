namespace EMG.Api.Modules.Steel.Dtos;

public sealed record CreateSteelRequest(
    int ProjectId,
    int? SupplierId,
    string OperationType,
    string? SteelType,
    string? BarDiameter,
    string? Grade,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    DateTime? OperationDate,
    string? DeliveryNoteNumber,
    string Description,
    string? Notes,
    string? AccountCategory = "الحديد",
    [EgpOnly] string? Currency = null);
