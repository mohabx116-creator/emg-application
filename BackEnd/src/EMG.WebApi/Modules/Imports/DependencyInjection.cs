using EMG.Api.Modules.Imports.Services;

namespace EMG.Api.Modules.Imports;

public static class DependencyInjection
{
    public static IServiceCollection AddImportsModule(this IServiceCollection services)
    {
        services.AddScoped<IWorkbookImportService, WorkbookImportService>();
        return services;
    }
}
