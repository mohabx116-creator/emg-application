using EMG.Api.Modules.Security.Dtos;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Security.Services;

public sealed class AccountService(
    EmgDbContext db,
    IPasswordHasher<AppUser> passwordHasher,
    IAuthSessionService sessions) : IAccountService
{
    public async Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.UserName == request.Email || item.Email == request.Email, cancellationToken);
        if (user is null || !user.IsActive) return null;

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed) return null;

        user.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var (session, secret) = await sessions.CreateAsync(user, request.RememberMe, cancellationToken);
        return new LoginResult(secret, user.Id.ToString(), user.Email, user.DisplayName, user.UserRoles.FirstOrDefault()?.Role.Name ?? "user", session.RememberMe);
    }
}
