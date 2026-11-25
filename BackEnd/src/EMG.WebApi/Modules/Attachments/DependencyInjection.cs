using EMG.Api.Modules.Attachments.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Attachments;

public static class DependencyInjection
{
    public static IServiceCollection AddAttachmentsModule(this IServiceCollection services)
    {
        services.AddScoped<IAttachmentsService, AttachmentsService>();
        return services;
    }
}
