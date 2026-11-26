using EMG.Api.Modules.SiteMovements.Dtos;

namespace EMG.Api.Modules.SiteMovements.Services;

public interface ISiteMovementsService
{
    Task<IReadOnlyList<SiteMovementSummary>> GetAsync(int? projectId, string? movementType, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<SiteMovementSummary?> GetByIdAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<SiteMovementSummary?> CreateAsync(CreateSiteMovementRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<IReadOnlyList<SiteMovementSummary>?> CreateBatchAsync(CreateSiteMovementBatchRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<SiteMovementSummary?> UpdateAsync(int id, CreateSiteMovementRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken, bool hasOverridePermission = false);
    Task<bool> DeleteAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken, bool hasOverridePermission = false);
}
