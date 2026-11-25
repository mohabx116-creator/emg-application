namespace EMG.Domain.Security;

public sealed class UserProjectAssignment
{
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int ProjectId { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
