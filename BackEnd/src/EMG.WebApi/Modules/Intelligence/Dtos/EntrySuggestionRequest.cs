namespace EMG.Api.Modules.Intelligence.Dtos;

public sealed record EntrySuggestionRequest(
    int? ProjectId,
    string Description,
    string? MovementType,
    string? Unit,
    string? SupplierName,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? Amount);
