using EMG.Api.Modules.Security.Dtos;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
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
        await transaction.CommitAsync(cancellationToken);
        return new UserSummary(user.Id, user.UserName, user.Email, user.DisplayName, user.IsActive, user.MustChangePassword, roles.Select(role => role.Name).ToArray(), []);
    }

    public async Task<UserSummary?> UpdateAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var roleNames = NormalizeRoleNames(request.Role, request.Roles);
        var roles = await EnsureRolesAsync(roleNames, cancellationToken);

        user.Email = request.Email.Trim();
        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.UserRoles.Clear();
        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync(cancellationToken);
        if (!user.IsActive) await sessions.RevokeAllAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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

    public async Task<bool?> DeleteUnusedAsync(int userId, int actorId, CancellationToken ct)
    {
        if (userId == actorId) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == userId, ct);
        if (user is null) return null;
        if (user.IsActive || user.LastLoginAtUtc.HasValue || user.ProfileImagePath is not null ||
            user.UserRoles.Any(link => link.Role.Name == "admin") || await HasAccountHistoryAsync(userId, ct))
            return false;

        db.Users.Remove(user);
        db.SecurityAuditLogs.Add(new SecurityAuditLog
        {
            UserId = actorId, Action = "user.deleted", EntityType = "user", EntityId = userId,
            Details = "حذف حساب غير نشط لم يسبق استخدامه."
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private async Task<bool> HasAccountHistoryAsync(int id, CancellationToken ct) =>
        await db.UserProjectAssignments.AnyAsync(item => item.UserId == id, ct) ||
        await db.AuthSessions.AnyAsync(item => item.UserId == id, ct) ||
        await db.PasswordResetTokens.AnyAsync(item => item.UserId == id, ct) ||
        await db.Notifications.AnyAsync(item => item.UserId == id, ct) ||
        await db.SecurityAuditLogs.AnyAsync(item => item.UserId == id || item.EntityType == "user" && item.EntityId == id, ct) ||
        await db.AttendanceRecords.AnyAsync(item => item.UserId == id, ct) ||
        await db.WorkflowActions.AnyAsync(item => item.UserId == id, ct) ||
        await db.Attachments.AnyAsync(item => item.UploadedByUserId == id, ct) ||
        await db.SiteMovements.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.ExcavationRecords.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.ConcreteRecords.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.SteelRecords.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.FinishingRecords.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.AdvancePayments.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.SupplierSupplies.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.SupplierPayments.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.SupplierInvoices.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.ContractorContracts.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.ContractorStatements.AnyAsync(item => item.CreatedByUserId == id, ct) ||
        await db.HistoricalImportBatches.AnyAsync(item => item.CreatedByUserId == id || item.ApprovedByUserId == id, ct) ||
        await db.HistoricalImportAuditEvents.AnyAsync(item => item.ActorUserId == id, ct) ||
        await db.HistoricalPostedEntries.AnyAsync(item => item.PostedByUserId == id, ct);
}
