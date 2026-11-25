using EMG.Api.Modules.ReportBuilder.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.ReportBuilder;

public static class DependencyInjection
{
    public static IServiceCollection AddReportBuilderModule(this IServiceCollection services)
    {
        services.AddScoped<IReportBuilderService, ReportBuilderService>();
        return services;
    }
}
