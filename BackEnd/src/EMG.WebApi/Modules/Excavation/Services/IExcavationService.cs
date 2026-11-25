using EMG.Api.Modules.Excavation.Dtos;

namespace EMG.Api.Modules.Excavation.Services;

public interface IExcavationService
{
    Task<IReadOnlyList<ExcavationSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<ExcavationSummary?> CreateAsync(CreateExcavationRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
