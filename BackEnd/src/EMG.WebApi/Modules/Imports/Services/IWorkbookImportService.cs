using EMG.Api.Modules.Imports.Dtos;

namespace EMG.Api.Modules.Imports.Services;

public interface IWorkbookImportService
{
    Task<WorkbookPreview> PreviewAsync(IFormFile file, CancellationToken cancellationToken);
    Task<WorkbookMigrationPlan?> BuildNewProjectPlanAsync(IFormFile file, CancellationToken cancellationToken);
    Task<ImportedProjectResult?> CommitNewProjectAsync(IFormFile file, string? projectName, string? projectCode, string? clientName, string? location, string? engineerName, int? userId, CancellationToken cancellationToken);
    Task<WorkbookMigrationPlan?> BuildExistingProjectPlanAsync(IFormFile file, int projectId, CancellationToken cancellationToken);
    Task<ImportedProjectResult?> CommitExistingProjectAsync(IFormFile file, int projectId, int? userId, CancellationToken cancellationToken);
    Task<ImportedProjectResult> CommitApprovedHistoryAsync(IFormFile file, int? projectId, bool confirmedApproved, DateOnly? closedThrough,
        int userId, bool isCanonicalAdmin, CancellationToken cancellationToken);
}
