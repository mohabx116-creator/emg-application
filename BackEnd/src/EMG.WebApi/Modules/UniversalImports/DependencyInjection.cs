namespace EMG.Api.Modules.UniversalImports;

public static class DependencyInjection
{
    public static IServiceCollection AddUniversalImportModule(this IServiceCollection services)
    {
        services.AddSingleton<UniversalImportMappingRegistry>();
        services.AddScoped<IUniversalImportPreviewService, UniversalImportPreviewService>();
        services.AddScoped<IHistoricalImportPostingService, HistoricalImportPostingService>();
        services.AddSingleton<ReviewedWorkbookSnapshots>();
        services.AddScoped<ReviewedWorkbookImportService>();
        services.AddScoped<HistoricalBulkMigrationService>();
        return services;
    }
}
