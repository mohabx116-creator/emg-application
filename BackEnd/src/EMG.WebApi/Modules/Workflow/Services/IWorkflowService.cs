using EMG.Api.Modules.Workflow.Dtos;

namespace EMG.Api.Modules.Workflow.Services;

public interface IWorkflowService
{
    Task<WorkflowResult?> ExecuteAsync(string entityType, int entityId, string action, string? notes, int? userId, bool isCanonicalAdmin, CancellationToken cancellationToken);
    Task<bool> CorrectSupplyAsync(int entityId, CorrectSupplyRequest request, int? userId, bool isCanonicalAdmin, CancellationToken cancellationToken);
    Task<WorkflowAuditResult?> AuditAsync(string entityType, int entityId, int userId, bool isCanonicalAdmin, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowReviewItem>> GetReviewQueueAsync(int userId, bool isCanonicalAdmin, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowResult>> GetHistoryAsync(string entityType, int entityId, CancellationToken cancellationToken);
    Task<bool> CanViewHistoryAsync(string entityType, int entityId, int userId, bool isCanonicalAdmin, CancellationToken cancellationToken);
}
