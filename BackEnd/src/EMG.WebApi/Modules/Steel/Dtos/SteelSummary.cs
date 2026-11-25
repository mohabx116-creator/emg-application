namespace EMG.Api.Modules.Steel.Dtos;

public sealed record SteelSummary(
    int Id,
    int ProjectId,
    string ProjectName,
    int? SupplierId,
    string SupplierName,
    string AccountCategory,
    string OperationType,
    string SteelType,
    string BarDiameter,
    string Grade,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal TotalAmount,
    DateTime OperationDate,
    string DeliveryNoteNumber,
    string Description,
    string Status,
    string Notes);
