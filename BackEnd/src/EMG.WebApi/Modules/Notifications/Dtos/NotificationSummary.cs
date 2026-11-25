namespace EMG.Api.Modules.Notifications.Dtos;

public sealed record NotificationSummary(
    int Id,
    string Type,
    string Title,
    string Message,
    string EntityType,
    int? EntityId,
    bool IsRead,
    DateTime CreatedAtUtc);
