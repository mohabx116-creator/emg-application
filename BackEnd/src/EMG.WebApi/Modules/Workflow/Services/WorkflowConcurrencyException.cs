namespace EMG.Api.Modules.Workflow.Services;

public sealed class WorkflowConcurrencyException(string entityType, int entityId, string expectedStatus)
    : Exception("تغيرت حالة الحركة أثناء تنفيذ الإجراء، أعد المحاولة.")
{
    public string EntityType { get; } = entityType;
    public int EntityId { get; } = entityId;
    public string ExpectedStatus { get; } = expectedStatus;
}
