namespace EMG.Api.Modules.Excavation.Dtos;

public sealed record CreateExcavationRequest(
    int ProjectId,
    int? SupplierId,
    string CalculationMethod,
    decimal? DirectQuantity,
    decimal? Length,
    decimal? Width,
    decimal? Depth,
    string? Unit,
    decimal UnitPrice,
    string? SoilType,
    string Description,
    DateTime? ExcavationDate,
    string? Notes,
    string? AccountCategory = "العرب");
