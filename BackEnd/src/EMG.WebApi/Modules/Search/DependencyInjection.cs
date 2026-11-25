using EMG.Api.Modules.Search.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Search;

public static class DependencyInjection
{
    public static IServiceCollection AddSearchModule(this IServiceCollection services)
    {
        services.AddScoped<ISearchService, SearchService>();
        return services;
    }
}
