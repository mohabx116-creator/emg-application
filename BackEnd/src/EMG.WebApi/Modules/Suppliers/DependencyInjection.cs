using EMG.Api.Modules.Suppliers.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Suppliers;

public static class DependencyInjection
{
    public static IServiceCollection AddSuppliersModule(this IServiceCollection services)
    {
        services.AddScoped<ISuppliersService, SuppliersService>();
        return services;
    }
}
