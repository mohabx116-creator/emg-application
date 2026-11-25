namespace EMG.Api.Modules.Workflow.Dtos;

public sealed record WorkflowResult(
    string EntityType,
    int EntityId,
    string PreviousStatus,
    string CurrentStatus,
    string Action,
    DateTime ActionDateUtc);
