using System.Security.Cryptography;
using System.Text;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Security.Services;

public sealed class AuthSessionService(EmgDbContext db) : IAuthSessionService
{
    public async Task<(AuthSession Session, string Secret)> CreateAsync(AppUser user, bool rememberMe, CancellationToken cancellationToken)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var session = new AuthSession
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Hash(secret), RememberMe = rememberMe,
            ExpiresAtUtc = DateTime.UtcNow.Add(rememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromHours(8))
        };
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return (session, secret);
    }

    public Task<AppUser?> ValidateAsync(string secret, CancellationToken cancellationToken) =>
        db.AuthSessions.AsNoTracking().Include(item => item.User).ThenInclude(user => user.UserRoles).ThenInclude(link => link.Role)
            .Where(item => item.TokenHash == Hash(secret) && item.RevokedAtUtc == null && item.ExpiresAtUtc > DateTime.UtcNow && item.User.IsActive)
            .Select(item => item.User).SingleOrDefaultAsync(cancellationToken);

    public async Task RevokeAsync(string secret, CancellationToken cancellationToken)
    {
        var session = await db.AuthSessions.SingleOrDefaultAsync(item => item.TokenHash == Hash(secret) && item.RevokedAtUtc == null, cancellationToken);
        if (session is null) return;
        session.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(int userId, CancellationToken cancellationToken)
    {
        await db.AuthSessions.Where(item => item.UserId == userId && item.RevokedAtUtc == null).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAtUtc, DateTime.UtcNow), cancellationToken);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
