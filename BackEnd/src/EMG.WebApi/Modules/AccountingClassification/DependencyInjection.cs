using EMG.Api.Modules.AccountingClassification.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.AccountingClassification;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountingClassificationModule(this IServiceCollection services)
    {
        services.AddScoped<IAccountingClassificationService, AccountingClassificationService>();
        return services;
    }
}
