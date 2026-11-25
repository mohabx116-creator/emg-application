using EMG.Api.Modules.Projects.Repositories;
using EMG.Api.Modules.Projects.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Projects;

public static class DependencyInjection
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IProjectsRepository, ProjectsRepository>();
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("EmgDb")))
        {
            services.AddScoped<IProjectsRepository, EfProjectsRepository>();
        }
        services.AddScoped<IProjectsService, ProjectsService>();
        return services;
    }
}
