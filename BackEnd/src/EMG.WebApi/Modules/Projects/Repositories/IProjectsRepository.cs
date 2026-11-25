using EMG.Api.Modules.Projects.Dtos;

namespace EMG.Api.Modules.Projects.Repositories;

public interface IProjectsRepository
{
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<ProjectSummary?> GetProjectAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<ProjectSummary> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectSummary?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectSummary?> DecideImportAsync(int id, bool approve, string? notes, int? userId, CancellationToken cancellationToken);
}
