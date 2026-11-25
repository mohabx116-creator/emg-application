namespace EMG.Api.Modules.Excavation.Dtos;

public sealed record ExcavationSummary(
    int Id,
    int ProjectId,
    string ProjectName,
    int? SupplierId,
    string SupplierName,
    string AccountCategory,
    string CalculationMethod,
    decimal? Length,
    decimal? Width,
    decimal? Depth,
    decimal CalculatedQuantity,
    string Unit,
    decimal UnitPrice,
    decimal TotalAmount,
    string SoilType,
    string Description,
    DateTime ExcavationDate,
    string Status,
    string Notes);
