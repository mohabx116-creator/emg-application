using EMG.Api.Modules.SiteMovements.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.SiteMovements;

public static class DependencyInjection
{
    public static IServiceCollection AddSiteMovementsModule(this IServiceCollection services)
    {
        services.AddScoped<ISiteMovementsService, SiteMovementsService>();
        return services;
    }
}
