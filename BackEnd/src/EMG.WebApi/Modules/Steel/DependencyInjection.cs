using EMG.Api.Modules.Steel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Steel;

public static class DependencyInjection
{
    public static IServiceCollection AddSteelModule(this IServiceCollection services)
    {
        services.AddScoped<ISteelService, SteelService>();
        return services;
    }
}
