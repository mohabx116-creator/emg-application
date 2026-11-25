using EMG.Api.Modules.Concrete.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Concrete;

public static class DependencyInjection
{
    public static IServiceCollection AddConcreteModule(this IServiceCollection services)
    {
        services.AddScoped<IConcreteService, ConcreteService>();
        return services;
    }
}
