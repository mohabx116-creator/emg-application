using EMG.Api.Modules.Projects.Dtos;
using EMG.Api.Modules.Projects.Repositories;

namespace EMG.Api.Modules.Projects.Services;

public sealed class ProjectsService(IProjectsRepository repository) : IProjectsService
{
    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(int? userId, bool isAdmin, CancellationToken cancellationToken) => repository.GetProjectsAsync(userId, isAdmin, cancellationToken);

    public Task<ProjectSummary?> GetProjectAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken) => repository.GetProjectAsync(id, userId, isAdmin, cancellationToken);

    public Task<ProjectSummary> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken) => repository.CreateAsync(request, cancellationToken);

    public Task<ProjectSummary?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken) => repository.UpdateAsync(id, request, cancellationToken);

    public Task<ProjectSummary?> DecideImportAsync(int id, bool approve, string? notes, int? userId, CancellationToken cancellationToken) => repository.DecideImportAsync(id, approve, notes, userId, cancellationToken);
}
