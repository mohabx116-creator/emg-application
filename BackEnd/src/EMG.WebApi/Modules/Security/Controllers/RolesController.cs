using EMG.Api.Modules.Security.Dtos;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EMG.Api.Modules.Security.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace EMG.Api.Modules.Security.Controllers;

[ApiController]
[Authorize]
[Route("api/roles")]
public sealed class RolesController(EmgDbContext db) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("roles.view")]
    public async Task<ActionResult<IReadOnlyList<RolePermissionSummary>>> Get(CancellationToken cancellationToken) =>
        Ok(await db.Roles.AsNoTracking().Include(role => role.RolePermissions).ThenInclude(item => item.Permission).OrderBy(role => role.Name).Select(role => new RolePermissionSummary(
            role.Id, role.Name, role.DisplayName, role.RolePermissions.OrderBy(item => item.Permission.DisplayName).Select(item => new RolePermissionGrantSummary(item.Permission.Id, item.Permission.Code, item.Permission.DisplayName, item.Scope, item.AmountLimit)).ToArray())).ToListAsync(cancellationToken));

    [HttpGet("permissions")]
    [PermissionAuthorize("roles.view")]
    public async Task<ActionResult<IReadOnlyList<PermissionSummary>>> Permissions(CancellationToken cancellationToken) =>
        Ok(await db.Permissions.AsNoTracking().OrderBy(item => item.DisplayName).Select(item => new PermissionSummary(item.Id, item.Code, item.DisplayName)).ToListAsync(cancellationToken));

    [HttpPut("{roleId:int}/permissions")]
    [PermissionAuthorize("roles.update")]
    public async Task<IActionResult> Update(int roleId, UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.Include(item => item.RolePermissions).FirstOrDefaultAsync(item => item.Id == roleId, cancellationToken);
        if (role is null) return NotFound();
        // The built-in administrator role is the last-resort recovery role. It must not
        // be emptied or weakened by an ordinary role manager, even if that user has
        // roles.update. Only a true administrator may change it.
        if (role.Name.Equals("admin", StringComparison.OrdinalIgnoreCase) && !User.IsInRole("admin"))
            return Forbid();
        var requestedGrants = request.Grants is { Count: > 0 }
            ? request.Grants
            : request.PermissionIds.Select(permissionId => new RolePermissionGrantRequest(permissionId));
        if (!User.IsInRole("admin") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var managerId))
        {
            var managerPermissionIds = await db.UserRoles.Where(link => link.UserId == managerId)
                .SelectMany(link => link.Role.RolePermissions.Select(grant => grant.PermissionId)).Distinct().ToListAsync(cancellationToken);
            if (requestedGrants.Any(grant => !managerPermissionIds.Contains(grant.PermissionId))) return Forbid();
        }
        var validPermissions = await db.Permissions.Where(item => requestedGrants.Select(grant => grant.PermissionId).Contains(item.Id)).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(role.RolePermissions);
        db.RolePermissions.AddRange(requestedGrants
            .Where(grant => validPermissions.Any(permission => permission.Id == grant.PermissionId))
            .GroupBy(grant => grant.PermissionId)
            .Select(group => group.First())
            .Select(grant => new RolePermission
            {
                RoleId = roleId,
                PermissionId = grant.PermissionId,
                Scope = NormalizeScope(grant.Scope),
                AmountLimit = grant.AmountLimit is >= 0 ? grant.AmountLimit : null
            }));
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : (int?)null;
        db.SecurityAuditLogs.Add(new SecurityAuditLog
        {
            UserId = currentUserId,
            Action = "permissions.updated",
            EntityType = "role",
            EntityId = roleId,
            Details = JsonSerializer.Serialize(requestedGrants.Select(grant => new { grant.PermissionId, Scope = NormalizeScope(grant.Scope), grant.AmountLimit }))
        });
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("audit")]
    [PermissionAuthorize("roles.view")]
    public async Task<ActionResult<IReadOnlyList<object>>> Audit(CancellationToken cancellationToken) =>
        Ok(await db.SecurityAuditLogs.AsNoTracking().Include(item => item.User).OrderByDescending(item => item.CreatedAtUtc).Take(200)
            .Select(item => new { item.Id, item.Action, item.EntityType, item.EntityId, item.Details, item.CreatedAtUtc, UserName = item.User == null ? "" : item.User.DisplayName })
            .ToListAsync(cancellationToken));

    private static string NormalizeScope(string? scope) => scope?.Trim().ToLowerInvariant() switch
    {
        "assigned" or "assigned-projects" => "assigned",
        "own" or "own-records" => "own",
        _ => "all"
    };
}
