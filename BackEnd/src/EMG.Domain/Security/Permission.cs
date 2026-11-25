namespace EMG.Domain.Security;

public sealed class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
