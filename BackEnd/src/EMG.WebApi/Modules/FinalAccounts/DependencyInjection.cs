using EMG.Api.Modules.FinalAccounts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.FinalAccounts;

public static class DependencyInjection
{
    public static IServiceCollection AddFinalAccountsModule(this IServiceCollection services)
    {
        services.AddScoped<IFinalAccountsService, FinalAccountsService>();
        return services;
    }
}
