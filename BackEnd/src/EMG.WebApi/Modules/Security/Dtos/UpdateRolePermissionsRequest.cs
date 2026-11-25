namespace EMG.Api.Modules.Security.Dtos;

public sealed record RolePermissionGrantRequest(int PermissionId, string? Scope = "all", decimal? AmountLimit = null);
public sealed record UpdateRolePermissionsRequest(IReadOnlyList<int> PermissionIds, IReadOnlyList<RolePermissionGrantRequest>? Grants = null);
