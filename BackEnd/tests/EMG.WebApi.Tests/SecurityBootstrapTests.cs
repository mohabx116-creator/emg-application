using EMG.Api.Modules.Security.Data;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EMG.WebApi.Tests;

public sealed class SecurityBootstrapTests
{
    [Fact]
    public async Task ProductionWithoutExplicitBootstrapConfigurationDoesNotCreateAdministrator()
    {
        await using var db = CreateDbContext();
        var result = await SecurityBootstrapper.BootstrapAdministratorAsync(
            db,
            new ConfigurationBuilder().Build(),
            CreateEnvironment(Environments.Production),
            new Role { Name = "admin", DisplayName = "Administrator" });

        Assert.Null(result);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task ExplicitBootstrapCreatesFirstAdministratorWithChangeRequired()
    {
        await using var db = CreateDbContext();
        var password = $"Test-{Guid.NewGuid():N}-Strong!";
        var adminRole = new Role { Name = "admin", DisplayName = "Administrator" };
        db.Roles.Add(adminRole);
        var result = await SecurityBootstrapper.BootstrapAdministratorAsync(
            db,
            Configuration(new Dictionary<string, string?>
            {
                ["Security:BootstrapAdmin:Enabled"] = "true",
                ["Security:BootstrapAdmin:UserName"] = "bootstrap@example.test",
                ["Security:BootstrapAdmin:Email"] = "bootstrap@example.test",
                ["Security:BootstrapAdmin:DisplayName"] = "Bootstrap Administrator",
                ["Security:BootstrapAdmin:Password"] = password,
                ["Security:BootstrapAdmin:AllowInProduction"] = "true"
            }),
            CreateEnvironment(Environments.Production),
            adminRole);

        Assert.NotNull(result);
        await db.SaveChangesAsync();
        Assert.True(result!.MustChangePassword);
        Assert.Equal(1, await db.UserRoles.CountAsync(item => item.Role.Name == "admin"));
        Assert.True(new PasswordHasher<AppUser>().VerifyHashedPassword(
            result,
            result.PasswordHash,
            password) == PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task ExistingUserIsNotRecreatedOrPasswordReplaced()
    {
        await using var db = CreateDbContext();
        var originalPassword = $"Original-{Guid.NewGuid():N}-Strong!";
        var replacementPassword = $"Replacement-{Guid.NewGuid():N}-Strong!";
        var existing = new AppUser
        {
            UserName = "existing@example.test",
            Email = "existing@example.test",
            DisplayName = "Existing Administrator"
        };
        existing.PasswordHash = new PasswordHasher<AppUser>()
            .HashPassword(existing, originalPassword);
        db.Users.Add(existing);
        await db.SaveChangesAsync();

        var result = await SecurityBootstrapper.BootstrapAdministratorAsync(
            db,
            Configuration(new Dictionary<string, string?>
            {
                ["Security:BootstrapAdmin:Enabled"] = "true",
                ["Security:BootstrapAdmin:UserName"] = existing.UserName,
                ["Security:BootstrapAdmin:Email"] = existing.Email,
                ["Security:BootstrapAdmin:DisplayName"] = "Replacement Must Not Happen",
                ["Security:BootstrapAdmin:Password"] = replacementPassword,
                ["Security:BootstrapAdmin:AllowInProduction"] = "true"
            }),
            CreateEnvironment(Environments.Production),
            new Role { Name = "admin", DisplayName = "Administrator" });

        Assert.Null(result);
        Assert.Single(db.Users);
        var unchanged = await db.Users.SingleAsync();
        Assert.Equal("Existing Administrator", unchanged.DisplayName);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>()
            .VerifyHashedPassword(unchanged, unchanged.PasswordHash, originalPassword));
        Assert.Equal(PasswordVerificationResult.Failed, new PasswordHasher<AppUser>()
            .VerifyHashedPassword(unchanged, unchanged.PasswordHash, replacementPassword));
    }

    [Fact]
    public async Task ExistingAdministratorWithDifferentIdentityBlocksBootstrap()
    {
        await using var db = CreateDbContext();
        var adminRole = new Role { Name = "admin", DisplayName = "Administrator" };
        var existing = new AppUser
        {
            UserName = "legitimate-admin@example.test",
            Email = "legitimate-admin@example.test",
            DisplayName = "Existing Administrator",
            PasswordHash = "existing-hash"
        };
        db.Roles.Add(adminRole);
        db.Users.Add(existing);
        db.UserRoles.Add(new UserRole { User = existing, Role = adminRole });
        await db.SaveChangesAsync();

        var result = await SecurityBootstrapper.BootstrapAdministratorAsync(
            db,
            Configuration(new Dictionary<string, string?>
            {
                ["Security:BootstrapAdmin:Enabled"] = "true",
                ["Security:BootstrapAdmin:UserName"] = "another-admin@example.test",
                ["Security:BootstrapAdmin:Email"] = "another-admin@example.test",
                ["Security:BootstrapAdmin:DisplayName"] = "Must Not Be Created",
                ["Security:BootstrapAdmin:Password"] = $"Test-{Guid.NewGuid():N}-Strong!",
                ["Security:BootstrapAdmin:AllowInProduction"] = "true"
            }),
            CreateEnvironment(Environments.Production),
            adminRole);

        Assert.Null(result);
        Assert.Single(db.Users);
        Assert.Single(db.UserRoles);
    }

    [Fact]
    public async Task InterruptedRelationalBootstrapCanBeRetriedWithoutAOrphanedUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var baseOptions = new DbContextOptionsBuilder<EmgDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setup = new EmgDbContext(baseOptions))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Roles.Add(new Role { Name = "admin", DisplayName = "Administrator" });
            await setup.SaveChangesAsync();
        }

        var failingOptions = new DbContextOptionsBuilder<EmgDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new FailOnSecondWriteCommandInterceptor())
            .Options;

        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Security:BootstrapAdmin:Enabled"] = "true",
            ["Security:BootstrapAdmin:UserName"] = "retry-admin@example.test",
            ["Security:BootstrapAdmin:Email"] = "retry-admin@example.test",
            ["Security:BootstrapAdmin:DisplayName"] = "Retry Administrator",
            ["Security:BootstrapAdmin:Password"] = $"Test-{Guid.NewGuid():N}-Strong!",
            ["Security:BootstrapAdmin:AllowInProduction"] = "true"
        });

        await using (var firstAttempt = new EmgDbContext(failingOptions))
        {
            var adminRole = await firstAttempt.Roles.SingleAsync(role => role.Name == "admin");
            var user = await SecurityBootstrapper.BootstrapAdministratorAsync(
                firstAttempt, configuration, CreateEnvironment(Environments.Production), adminRole);
            Assert.NotNull(user);
            await Assert.ThrowsAsync<DbUpdateException>(() => firstAttempt.SaveChangesAsync());
        }

        await using (var afterFailure = new EmgDbContext(baseOptions))
        {
            Assert.Empty(await afterFailure.Users.ToListAsync());
            Assert.Empty(await afterFailure.UserRoles.ToListAsync());
            Assert.Single(await afterFailure.Roles.ToListAsync());
        }

        await using (var retry = new EmgDbContext(baseOptions))
        {
            var persistedRole = await retry.Roles.SingleAsync(role => role.Name == "admin");
            var user = await SecurityBootstrapper.BootstrapAdministratorAsync(
                retry, configuration, CreateEnvironment(Environments.Production), persistedRole);
            Assert.NotNull(user);
            await retry.SaveChangesAsync();
            Assert.Single(await retry.Users.ToListAsync());
            Assert.Single(await retry.UserRoles.ToListAsync());
        }
    }

    [Fact]
    public void ProductionBootstrapRequiresAnExplicitProductionOverride()
    {
        var password = $"Test-{Guid.NewGuid():N}-Strong!";
        var options = AdminBootstrapOptions.FromConfiguration(Configuration(new Dictionary<string, string?>
        {
            ["Security:BootstrapAdmin:Enabled"] = "true",
            ["Security:BootstrapAdmin:UserName"] = "bootstrap@example.test",
            ["Security:BootstrapAdmin:Email"] = "bootstrap@example.test",
            ["Security:BootstrapAdmin:DisplayName"] = "Bootstrap Administrator",
            ["Security:BootstrapAdmin:Password"] = password
        }));

        Assert.Throws<InvalidOperationException>(() =>
            options.Validate(CreateEnvironment(Environments.Production)));
    }

    private static EmgDbContext CreateDbContext() => new(new DbContextOptionsBuilder<EmgDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class FailOnSecondWriteCommandInterceptor : DbCommandInterceptor
    {
        private int _writeCount;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowOnSecondWrite(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowOnSecondWrite(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void ThrowOnSecondWrite(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase) &&
                Interlocked.Increment(ref _writeCount) == 2)
                throw new InvalidOperationException("Simulated mid-save failure.");
        }
    }

    private static IConfiguration Configuration(IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment CreateEnvironment(string environmentName) => new TestHostEnvironment
    {
        EnvironmentName = environmentName,
        ApplicationName = typeof(SecurityBootstrapTests).Assembly.GetName().Name!
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
