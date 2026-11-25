namespace EMG.Api.Modules.Search.Dtos;

public sealed record SearchResult(
    string EntityType,
    int EntityId,
    string Title,
    string Subtitle,
    int? ProjectId,
    string Status);
