using EMG.Api.Modules.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
