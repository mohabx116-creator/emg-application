namespace EMG.Api.Modules.Workflow.Dtos;

public sealed record WorkflowReviewItem(
    string EntityType,
    int EntityId,
    int ProjectId,
    string ProjectName,
    string Description,
    decimal Amount,
    string Status,
    DateTime Date,
    bool HasAttachment,
    bool CanApprove,
    bool CanPost,
    IReadOnlyList<string> BlockingChecks,
    IReadOnlyList<string> Warnings)
{
    public bool CanAcceptTechnically { get; init; }
    public bool CanApproveFinancially { get; init; }
    public bool CanReject { get; init; }
}
