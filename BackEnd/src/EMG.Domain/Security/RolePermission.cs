namespace EMG.Domain.Security;

public sealed class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
    public string Scope { get; set; } = "all";
    public decimal? AmountLimit { get; set; }
}
