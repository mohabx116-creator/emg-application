namespace EMG.Domain.Security;

public sealed class AuthSession
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public bool RememberMe { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public AppUser User { get; set; } = null!;
}
