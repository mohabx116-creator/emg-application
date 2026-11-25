using EMG.Api.Modules.ConsolidatedStatements.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.ConsolidatedStatements;

public static class DependencyInjection
{
    public static IServiceCollection AddConsolidatedStatementsModule(this IServiceCollection services)
    {
        services.AddScoped<IConsolidatedStatementService, ConsolidatedStatementService>();
        return services;
    }
}
