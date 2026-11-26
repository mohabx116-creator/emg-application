using EMG.Api.Modules.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Transactions;

public static class DependencyInjection
{
    public static IServiceCollection AddTransactionCorrections(this IServiceCollection services)
    {
        services.AddScoped<ITransactionCorrectionService, TransactionCorrectionService>();
        return services;
    }
}
