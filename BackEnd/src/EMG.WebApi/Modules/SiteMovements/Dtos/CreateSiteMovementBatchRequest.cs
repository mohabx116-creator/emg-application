namespace EMG.Api.Modules.SiteMovements.Dtos;

public sealed record CreateSiteMovementBatchRequest(IReadOnlyList<CreateSiteMovementRequest> Movements);
