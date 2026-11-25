using EMG.Api.Modules.Finishing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Finishing;

public static class DependencyInjection
{
    public static IServiceCollection AddFinishingModule(this IServiceCollection services)
    {
        services.AddScoped<IFinishingService, FinishingService>();
        return services;
    }
}
