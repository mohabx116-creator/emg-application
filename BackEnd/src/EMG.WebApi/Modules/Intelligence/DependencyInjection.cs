using EMG.Api.Modules.Intelligence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Intelligence;

public static class DependencyInjection
{
    public static IServiceCollection AddIntelligenceModule(this IServiceCollection services)
    {
        services.AddScoped<IEntryIntelligenceService, EntryIntelligenceService>();
        return services;
    }
}
