using EMG.Api.Modules.Finishing.Dtos;

namespace EMG.Api.Modules.Finishing.Services;

public interface IFinishingService
{
    Task<IReadOnlyList<FinishingSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<FinishingSummary?> CreateAsync(CreateFinishingRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
