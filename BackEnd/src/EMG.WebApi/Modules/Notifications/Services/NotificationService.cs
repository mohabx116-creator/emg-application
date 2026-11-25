using EMG.Api.Modules.Notifications.Dtos;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Notifications.Services;

public sealed class NotificationService(EmgDbContext db) : INotificationService
{
    public async Task<IReadOnlyList<NotificationSummary>> GetAsync(int userId, bool unreadOnly, CancellationToken cancellationToken)
    {
        var query = db.Notifications.AsNoTracking().Where(item => item.UserId == userId);
        if (unreadOnly) query = query.Where(item => !item.IsRead);
        return await query.OrderByDescending(item => item.CreatedAtUtc).Take(100)
            .Select(item => new NotificationSummary(item.Id, item.Type, item.Title, item.Message, item.EntityType ?? string.Empty, item.EntityId, item.IsRead, item.CreatedAtUtc)).ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken)
    {
        var affected = await db.Notifications.Where(item => item.Id == notificationId && item.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsRead, true), cancellationToken);
        return affected > 0;
    }

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken) =>
        await db.Notifications.Where(item => item.UserId == userId && !item.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsRead, true), cancellationToken);

    public async Task NotifyUserAsync(int userId, string type, string title, string message, string? entityType, int? entityId, CancellationToken cancellationToken)
    {
        db.Notifications.Add(new Notification { UserId = userId, Type = type, Title = title, Message = message, EntityType = entityType, EntityId = entityId });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyAdminsAsync(string type, string title, string message, string? entityType, int? entityId, CancellationToken cancellationToken)
    {
        var adminIds = await db.UserRoles.Where(item => item.Role.Name == "admin").Select(item => item.UserId).Distinct().ToListAsync(cancellationToken);
        foreach (var adminId in adminIds) db.Notifications.Add(new Notification { UserId = adminId, Type = type, Title = title, Message = message, EntityType = entityType, EntityId = entityId });
        await db.SaveChangesAsync(cancellationToken);
    }
}
