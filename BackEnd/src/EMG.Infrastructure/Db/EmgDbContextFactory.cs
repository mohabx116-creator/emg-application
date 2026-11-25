using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace EMG.Infrastructure.Db;

public sealed class EmgDbContextFactory : IDesignTimeDbContextFactory<EmgDbContext>
{
    public EmgDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__EmgDb")
            ?? throw new InvalidOperationException("ConnectionStrings__EmgDb must be supplied through the environment.");
        var host = Environment.GetEnvironmentVariable("EMG_MIGRATION_HOST");
        var database = Environment.GetEnvironmentVariable("EMG_MIGRATION_DATABASE");
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(host) || host.Contains(',') ||
            string.IsNullOrWhiteSpace(database) ||
            !int.TryParse(Environment.GetEnvironmentVariable("EMG_MIGRATION_PORT"), out var port) ||
            port is < 1 or > 65535 or 5432 ||
            !string.Equals(connection.Host, host, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(connection.Database, database, StringComparison.Ordinal) ||
            connection.Port != port)
            throw new InvalidOperationException("Supply an explicit matching RC migration host, database and non-default port.");

        var options = new DbContextOptionsBuilder<EmgDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EmgDbContext(options);
    }
}
