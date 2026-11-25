namespace EMG.Api.Modules.Concrete.Dtos;

public sealed record ConcreteSummary(
    int Id,
    int ProjectId,
    string ProjectName,
    int? SupplierId,
    string SupplierName,
    string AccountCategory,
    string ConcreteType,
    string ElementType,
    string MixGrade,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal TotalAmount,
    DateTime PourDate,
    string DeliveryNoteNumber,
    string Description,
    string Status,
    string Notes);
