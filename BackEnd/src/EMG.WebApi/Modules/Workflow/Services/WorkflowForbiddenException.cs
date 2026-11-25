namespace EMG.Api.Modules.Workflow.Services;

public sealed class WorkflowForbiddenException(string message) : Exception(message);
