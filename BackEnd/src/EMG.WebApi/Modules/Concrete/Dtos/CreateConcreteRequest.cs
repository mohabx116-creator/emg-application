namespace EMG.Api.Modules.Concrete.Dtos;

public sealed record CreateConcreteRequest(
    int ProjectId,
    int? SupplierId,
    string ConcreteType,
    string ElementType,
    string? MixGrade,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    DateTime? PourDate,
    string? DeliveryNoteNumber,
    string Description,
    string? Notes,
    string? AccountCategory = "المقاول");
