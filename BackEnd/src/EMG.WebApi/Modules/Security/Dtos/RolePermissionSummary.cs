namespace EMG.Api.Modules.Security.Dtos;

public sealed record PermissionSummary(int Id, string Code, string DisplayName);
public sealed record RolePermissionGrantSummary(int Id, string Code, string DisplayName, string Scope, decimal? AmountLimit);
public sealed record RolePermissionSummary(int Id, string Name, string DisplayName, IReadOnlyList<RolePermissionGrantSummary> Permissions);
