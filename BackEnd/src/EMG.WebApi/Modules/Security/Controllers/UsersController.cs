using EMG.Api.Modules.Security.Dtos;
using EMG.Api.Modules.Security.Services;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Security.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(IUsersService usersService, EmgDbContext db) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("users.view")]
    public async Task<ActionResult<IReadOnlyList<UserSummary>>> GetUsers(CancellationToken cancellationToken) =>
        Ok(await usersService.GetUsersAsync(cancellationToken));

    [HttpPost]
    [PermissionAuthorize("users.create")]
    public async Task<ActionResult<UserSummary>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "اسم المستخدم والبريد والاسم الظاهر وكلمة المرور المؤقتة مطلوبة." });
        if (!await CanManageRolesAsync(RoleNames(request.Role, request.Roles), cancellationToken)) return Forbid();
        return Ok(await usersService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{userId:int}")]
    [PermissionAuthorize("users.update")]
    public async Task<ActionResult<UserSummary>> UpdateUser(int userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName) || (request.Roles is null && string.IsNullOrWhiteSpace(request.Role)) || request.Roles is { Count: 0 })
            return BadRequest(new { message = "البريد والاسم الظاهر ودور واحد على الأقل مطلوبون." });
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId) &&
            currentUserId == userId && !request.IsActive)
            return BadRequest(new { message = "لا يمكن تعطيل الحساب المستخدم حاليًا من داخله." });
        var requestedRoleNames = RoleNames(request.Role, request.Roles);
        var existingRoleNames = await GetUserRoleNamesAsync(userId, cancellationToken);
        if (currentUserId == userId)
        {
            if (!existingRoleNames.ToHashSet(StringComparer.Ordinal).SetEquals(requestedRoleNames)) return Forbid();
        }
        else if (!await CanManageRolesAsync(existingRoleNames.Concat(requestedRoleNames).Distinct().ToArray(), cancellationToken))
        {
            return Forbid();
        }

        var user = await usersService.UpdateAsync(userId, request, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("{userId:int}/projects/{projectId:int}")]
    [PermissionAuthorize("users.assign-project")]
    public async Task<IActionResult> AssignProject(int userId, int projectId, CancellationToken cancellationToken) =>
        await usersService.AssignProjectAsync(userId, projectId, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{userId:int}/projects/{projectId:int}")]
    [PermissionAuthorize("users.assign-project")]
    public async Task<IActionResult> UnassignProject(int userId, int projectId, CancellationToken cancellationToken) =>
        await usersService.UnassignProjectAsync(userId, projectId, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{userId:int}/reset-password")]
    [PermissionAuthorize("users.reset-password")]
    public async Task<IActionResult> ResetPassword(int userId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new { message = "يجب توفير كلمة مرور مؤقتة عبر قناة آمنة." });
        if (!await CanManageRolesAsync(await GetUserRoleNamesAsync(userId, cancellationToken), cancellationToken)) return Forbid();
        return await usersService.ResetPasswordAsync(userId, request.Password, cancellationToken)
            ? Ok(new { message = "تم إعادة تعيين كلمة المرور، ويجب تغييرها عند أول دخول." })
            : NotFound();
    }

    [HttpDelete("{userId:int}")]
    [Authorize(Roles = "admin")]
    [PermissionAuthorize("users.update")]
    public async Task<IActionResult> DeleteUnused(int userId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Forbid();
        return await usersService.DeleteUnusedAsync(userId, actor, ct) switch
        {
            true => NoContent(),
            null => NotFound(),
            false => Conflict(new { message = "يمكن حذف الحسابات غير النشطة التي لم تُستخدم فقط. احتفظ بالحسابات ذات السجل معطّلة." })
        };
    }

    private Task<string[]> GetUserRoleNamesAsync(int userId, CancellationToken ct) =>
        db.UserRoles.Where(link => link.UserId == userId).Select(link => link.Role.Name).ToArrayAsync(ct);

    private async Task<bool> CanManageRolesAsync(string[] names, CancellationToken ct)
    {
        if (User.IsInRole("admin")) return true;
        if (names.Contains("admin") || !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var caller)) return false;
        var roles = await db.Roles.Where(role => names.Contains(role.Name)).CountAsync(ct);
        if (roles != names.Length) return false;
        var callerGrants = await db.UserRoles.Where(link => link.UserId == caller && link.User.IsActive)
            .SelectMany(link => link.Role.RolePermissions.Select(grant => new { grant.PermissionId, grant.Scope, grant.AmountLimit }))
            .ToListAsync(ct);
        var requestedGrants = await db.Roles.Where(role => names.Contains(role.Name))
            .SelectMany(role => role.RolePermissions.Select(grant => new { grant.PermissionId, grant.Scope, grant.AmountLimit }))
            .ToListAsync(ct);
        return requestedGrants.All(requested => callerGrants.Any(grant => grant.PermissionId == requested.PermissionId &&
            grant.Scope == requested.Scope && (grant.AmountLimit is null ||
                requested.AmountLimit is not null && grant.AmountLimit >= requested.AmountLimit)));
    }

    private static string[] RoleNames(string? role, IReadOnlyList<string>? roles)
    {
        var values = (roles is { Count: > 0 } ? roles : [role ?? ""])
            .Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim().ToLowerInvariant()).Distinct().ToArray();
        return values.Length == 0 ? ["user"] : values;
    }
}
