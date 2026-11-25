namespace EMG.Api.Modules.Projects.Dtos;

public sealed record UpdateProjectRequest(
    string Name,
    string Code,
    string? ClientName,
    string? OwnerName,
    string? PayerName,
    string? Location,
    string? Status,
    string? EngineerName,
    decimal OfficeSharePercentage);
