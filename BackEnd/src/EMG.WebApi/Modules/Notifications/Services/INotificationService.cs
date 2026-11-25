using EMG.Api.Modules.Notifications.Dtos;

namespace EMG.Api.Modules.Notifications.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationSummary>> GetAsync(int userId, bool unreadOnly, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken);
    Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken);
    Task NotifyUserAsync(int userId, string type, string title, string message, string? entityType, int? entityId, CancellationToken cancellationToken);
    Task NotifyAdminsAsync(string type, string title, string message, string? entityType, int? entityId, CancellationToken cancellationToken);
}
