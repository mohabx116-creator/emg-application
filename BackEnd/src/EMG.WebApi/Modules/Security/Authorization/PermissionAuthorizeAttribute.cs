using Microsoft.AspNetCore.Authorization;

namespace EMG.Api.Modules.Security.Authorization;

public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
{
    public PermissionAuthorizeAttribute(string permissionCode) => Policy = $"Permission:{permissionCode}";
}
