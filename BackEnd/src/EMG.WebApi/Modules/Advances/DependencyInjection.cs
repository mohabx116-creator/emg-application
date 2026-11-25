using EMG.Api.Modules.Advances.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Advances;

public static class DependencyInjection
{
    public static IServiceCollection AddAdvancesModule(this IServiceCollection services)
    {
        services.AddScoped<IAdvancesService, AdvancesService>();
        return services;
    }
}
