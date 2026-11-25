using EMG.Api.Modules.Reports.Dtos;

namespace EMG.Api.Modules.Reports.Services;

public interface IReportsService
{
    Task<ProjectReportSummary?> GetProjectReportAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<string?> ExportProjectMovementsCsvAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<byte[]?> ExportProjectWorkbookAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<OperationalAuditSummary?> GetOperationalAuditAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<HistoricalProjectReport?> GetHistoricalProjectReportAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
