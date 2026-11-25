using EMG.Api.Modules.Reports.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Reports;

public static class DependencyInjection
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.AddScoped<IReportsService, ReportsService>();
        return services;
    }
}
