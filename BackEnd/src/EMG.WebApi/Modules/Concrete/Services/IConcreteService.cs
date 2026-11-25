using EMG.Api.Modules.Concrete.Dtos;

namespace EMG.Api.Modules.Concrete.Services;

public interface IConcreteService
{
    Task<IReadOnlyList<ConcreteSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<ConcreteSummary?> CreateAsync(CreateConcreteRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
