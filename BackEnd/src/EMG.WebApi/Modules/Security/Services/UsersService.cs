using EMG.Api.Modules.Security.Dtos;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Security.Services;

public sealed class UsersService(
    EmgDbContext db,
    IPasswordHasher<AppUser> passwordHasher,
    IAuthSessionService sessions) : IUsersService
{
    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .Include(user => user.ProjectAssignments)
            .OrderBy(user => user.DisplayName)
            .Select(user => new UserSummary(
                user.Id, user.UserName, user.Email, user.DisplayName, user.IsActive,
                user.MustChangePassword,
                user.UserRoles.Select(userRole => userRole.Role.Name).ToArray(),
                user.ProjectAssignments.Where(assignment => assignment.IsActive).Select(assignment => assignment.ProjectId).ToArray()))
            .ToListAsync(cancellationToken);

    public async Task<UserSummary> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("An explicit temporary password is required.", nameof(request));
        var roleNames = NormalizeRoleNames(request.Role, request.Roles);
        var roles = await EnsureRolesAsync(roleNames, cancellationToken);

        var user = new AppUser
        {
            UserName = request.UserName.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim(),
            IsActive = true, MustChangePassword = true, CreatedAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        db.UserRoles.AddRange(roles.Select(role => new UserRole { UserId = user.Id, RoleId = role.Id }));
        await db.SaveChangesAsync(cancellationToken);
        return new UserSummary(user.Id, user.UserName, user.Email, user.DisplayName, user.IsActive, user.MustChangePassword, roles.Select(role => role.Name).ToArray(), []);
    }

    public async Task<UserSummary?> UpdateAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return null;

        var roleNames = NormalizeRoleNames(request.Role, request.Roles);
        var roles = await EnsureRolesAsync(roleNames, cancellationToken);

        user.Email = request.Email.Trim();
        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.UserRoles.Clear();
        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync(cancellationToken);

        return await db.Users.AsNoTracking()
            .Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .Include(item => item.ProjectAssignments)
            .Where(item => item.Id == userId)
            .Select(item => new UserSummary(
                item.Id, item.UserName, item.Email, item.DisplayName, item.IsActive,
                item.MustChangePassword,
                item.UserRoles.Select(userRole => userRole.Role.Name).ToArray(),
                item.ProjectAssignments.Where(assignment => assignment.IsActive).Select(assignment => assignment.ProjectId).ToArray()))
            .SingleAsync(cancellationToken);
    }

    private async Task<List<Role>> EnsureRolesAsync(IEnumerable<string> roleNames, CancellationToken cancellationToken)
    {
        var names = roleNames.ToArray();
        var roles = await db.Roles.Where(role => names.Contains(role.Name)).ToListAsync(cancellationToken);
        foreach (var name in names.Except(roles.Select(role => role.Name), StringComparer.OrdinalIgnoreCase))
        {
            var role = new Role { Name = name, DisplayName = name };
            db.Roles.Add(role);
            roles.Add(role);
        }

        if (roles.Any(role => role.Id == 0)) await db.SaveChangesAsync(cancellationToken);
        return names.Select(name => roles.Single(role => role.Name == name)).ToList();
    }

    private static IReadOnlyList<string> NormalizeRoleNames(string? legacyRole, IReadOnlyList<string>? roles)
    {
        var source = roles is { Count: > 0 } ? roles : [legacyRole ?? string.Empty];
        var names = source
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names.Length == 0 ? ["user"] : names;
    }

    public async Task<bool> AssignProjectAsync(int userId, int projectId, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(user => user.Id == userId, cancellationToken) ||
            !await db.Projects.AnyAsync(project => project.Id == projectId, cancellationToken)) return false;
        var assignment = await db.UserProjectAssignments.FindAsync([userId, projectId], cancellationToken);
        if (assignment is null) db.UserProjectAssignments.Add(new UserProjectAssignment { UserId = userId, ProjectId = projectId });
        else assignment.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnassignProjectAsync(int userId, int projectId, CancellationToken cancellationToken)
    {
        var assignment = await db.UserProjectAssignments.FindAsync([userId, projectId], cancellationToken);
        if (assignment is null) return false;
        assignment.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.MustChangePassword = true;
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
