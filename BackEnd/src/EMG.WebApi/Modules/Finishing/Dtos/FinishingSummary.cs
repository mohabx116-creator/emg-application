namespace EMG.Api.Modules.Finishing.Dtos;

public sealed record FinishingSummary(
    int Id,
    int ProjectId,
    string ProjectName,
    int? SupplierId,
    string SupplierName,
    string AccountCategory,
    string WorkType,
    string AreaName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal TotalAmount,
    DateTime WorkDate,
    string Description,
    string Status,
    string Notes);
