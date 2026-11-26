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
        if (!User.IsInRole("admin") && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId))
        {
            IEnumerable<string> requestedRoleSource = request.Roles is { Count: > 0 } ? request.Roles : new[] { request.Role ?? string.Empty };
            var requestedRoleNames = requestedRoleSource
                .Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim().ToLowerInvariant()).Distinct().ToArray();
            var callerPermissionCodes = await db.UserRoles.Where(link => link.UserId == currentUserId)
                .SelectMany(link => link.Role.RolePermissions.Select(grant => grant.Permission.Code)).Distinct().ToListAsync(cancellationToken);
            var requestedPermissionCodes = await db.Roles.Where(role => requestedRoleNames.Contains(role.Name))
                .SelectMany(role => role.RolePermissions.Select(grant => grant.Permission.Code)).Distinct().ToListAsync(cancellationToken);
            if (requestedPermissionCodes.Any(permission => !callerPermissionCodes.Contains(permission))) return Forbid();
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
    public async Task<IActionResult> ResetPassword(int userId, ResetPasswordRequest request, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(request.Password)
            ? BadRequest(new { message = "يجب توفير كلمة مرور مؤقتة عبر قناة آمنة." })
            : await usersService.ResetPasswordAsync(userId, request.Password, cancellationToken)
                ? Ok(new { message = "تم إعادة تعيين كلمة المرور، ويجب تغييرها عند أول دخول." })
                : NotFound();
}
