namespace EMG.Api.Modules.Workflow.Dtos;

public sealed record WorkflowAuditResult(
    string EntityType,
    int EntityId,
    string Status,
    bool CanApprove,
    bool CanPost,
    IReadOnlyList<string> BlockingChecks,
    IReadOnlyList<string> Warnings)
{
    public bool CanSubmit { get; init; }
    public bool CanAcceptTechnically { get; init; }
    public bool CanApproveFinancially { get; init; }
    public bool CanReject { get; init; }
    public bool CanCorrectSupply { get; init; }
    public string? RejectionReason { get; init; }
}
