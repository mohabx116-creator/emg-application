using EMG.Api.Modules.Steel.Dtos;

namespace EMG.Api.Modules.Steel.Services;

public interface ISteelService
{
    Task<IReadOnlyList<SteelSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<SteelSummary?> CreateAsync(CreateSteelRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
