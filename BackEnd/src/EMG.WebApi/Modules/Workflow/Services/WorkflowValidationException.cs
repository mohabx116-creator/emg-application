namespace EMG.Api.Modules.Workflow.Services;

public sealed class WorkflowValidationException(IReadOnlyList<string> checks) : Exception
{
    public IReadOnlyList<string> Checks { get; } = checks;
}
