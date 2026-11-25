using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace EMG.Api.Modules.Security.Data;

/// <summary>
/// Performs an explicitly enabled, one-time administrator bootstrap.
/// It is intentionally a no-op unless all bootstrap settings are supplied.
/// </summary>
public static class SecurityBootstrapper
{
    public static async Task<AppUser?> BootstrapAdministratorAsync(
        EmgDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        Role administratorRole,
        CancellationToken cancellationToken = default)
    {
        var options = AdminBootstrapOptions.FromConfiguration(configuration);
        if (!options.Enabled) return null;

        options.Validate(environment);

        // Never modify an existing account. This protects existing installations
        // from password replacement or duplicate administrator creation.
        if (await db.UserRoles.AnyAsync(
                userRole => userRole.Role.Name == administratorRole.Name,
                cancellationToken) ||
            await db.Users.AnyAsync(
                user => user.UserName == options.UserName || user.Email == options.Email,
                cancellationToken))
            return null;

        var administrator = new AppUser
        {
            UserName = options.UserName,
            Email = options.Email,
            DisplayName = options.DisplayName,
            IsActive = true,
            MustChangePassword = true
        };
        administrator.PasswordHash = new PasswordHasher<AppUser>()
            .HashPassword(administrator, options.Password);

        db.Users.Add(administrator);
        // Keep the new user and its administrator role in the same unit of work.
        // The caller commits both with one SaveChangesAsync transaction.
        db.UserRoles.Add(new UserRole { User = administrator, Role = administratorRole });
        return administrator;
    }
}

public sealed record AdminBootstrapOptions(
    bool Enabled,
    string UserName,
    string Email,
    string DisplayName,
    string Password,
    bool AllowInProduction)
{
    public static AdminBootstrapOptions FromConfiguration(IConfiguration configuration) => new(
        configuration.GetValue("Security:BootstrapAdmin:Enabled", false),
        configuration["Security:BootstrapAdmin:UserName"] ?? string.Empty,
        configuration["Security:BootstrapAdmin:Email"] ?? string.Empty,
        configuration["Security:BootstrapAdmin:DisplayName"] ?? string.Empty,
        configuration["Security:BootstrapAdmin:Password"] ?? string.Empty,
        configuration.GetValue("Security:BootstrapAdmin:AllowInProduction", false));

    public void Validate(IHostEnvironment environment)
    {
        if (environment.IsProduction() && !AllowInProduction)
            throw new InvalidOperationException(
                "Administrator bootstrap is disabled in production unless explicitly allowed for a controlled one-time operation.");

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException(
                "Administrator bootstrap requires explicit username, email, display name, and password configuration.");

        if (Password.Length < 14)
            throw new InvalidOperationException(
                "Administrator bootstrap password must be at least 14 characters long.");
    }
}
