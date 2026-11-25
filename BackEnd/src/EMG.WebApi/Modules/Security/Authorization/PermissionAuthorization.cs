using System.Security.Claims;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMG.Api.Modules.Security.Authorization;

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public const string Prefix = "Permission:";
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return base.GetPolicyAsync(policyName);
        var code = policyName[Prefix.Length..];
        var policy = new AuthorizationPolicyBuilder().AddRequirements(new PermissionRequirement(code)).Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

public sealed class PermissionAuthorizationHandler(EmgDbContext db) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
        var allowed = await db.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Where(userRole => userRole.User.IsActive)
            .AnyAsync(userRole => userRole.Role.Name == "admin" || userRole.Role.RolePermissions.Any(link => link.Permission.Code == requirement.PermissionCode));
        if (allowed) context.Succeed(requirement);
    }
}
