using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEmgInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EmgDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'ConnectionStrings:EmgDb' is missing or empty. " +
                "Configure it via environment variable 'ConnectionStrings__EmgDb', dotnet user-secrets, " +
                "or a local untracked 'appsettings.Local.json' file before starting EMG.WebApi.");
        }

        services.AddDbContext<EmgDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
