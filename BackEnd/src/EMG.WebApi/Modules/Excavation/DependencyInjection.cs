using EMG.Api.Modules.Excavation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Excavation;

public static class DependencyInjection
{
    public static IServiceCollection AddExcavationModule(this IServiceCollection services)
    {
        services.AddScoped<IExcavationService, ExcavationService>();
        return services;
    }
}
