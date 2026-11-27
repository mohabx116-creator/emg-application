namespace EMG.Api.Modules.Finishing.Dtos;

public sealed record CreateFinishingRequest(
    int ProjectId,
    int? SupplierId,
    string WorkType,
    string? AreaName,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    DateTime? WorkDate,
    string Description,
    string? Notes,
    string? AccountCategory = "غير محدد",
    [EgpOnly] string? Currency = null);
