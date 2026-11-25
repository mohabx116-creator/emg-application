namespace EMG.Api.Modules.Projects.Dtos;

public sealed record ProjectSummary(
    int Id,
    string Name,
    string Code,
    string ClientName,
    string Location,
    string Status,
    string EngineerName,
    int OpenMovements,
    decimal TotalIssued,
    decimal TotalReceived);
