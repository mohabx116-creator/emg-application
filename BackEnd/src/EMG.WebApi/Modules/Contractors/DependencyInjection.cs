using EMG.Api.Modules.Contractors.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Contractors;

public static class DependencyInjection
{
    public static IServiceCollection AddContractorsModule(this IServiceCollection services)
    {
        services.AddScoped<IContractorStatementService, ContractorStatementService>();
        return services;
    }
}
