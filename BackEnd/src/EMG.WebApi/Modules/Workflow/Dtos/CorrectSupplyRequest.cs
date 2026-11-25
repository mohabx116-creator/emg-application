using System.ComponentModel.DataAnnotations;

namespace EMG.Api.Modules.Workflow.Dtos;

public sealed record CorrectSupplyRequest(
    [param: StringLength(100)] string SupplyType,
    [param: StringLength(300)] string ItemName,
    [param: StringLength(80)] string Unit,
    decimal Quantity,
    decimal UnitPrice,
    [param: StringLength(120)] string? InvoiceNumber);
