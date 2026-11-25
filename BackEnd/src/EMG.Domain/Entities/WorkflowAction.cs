namespace EMG.Domain.Entities;

public sealed class WorkflowAction
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public DateTime ActionDateUtc { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
