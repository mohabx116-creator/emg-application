using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using EMG.Api.Modules.Security;
using EMG.Api.Modules.Security.Dtos;
using EMG.Api.Modules.Security.Services;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class AuthSecurityRegressionTests
{
    [Fact]
    public async Task UserCreationRequiresAnExplicitTemporaryPassword()
    {
        await using var db = CreateSqliteDb();
        var service = new UsersService(db, new PasswordHasher<AppUser>(), new RecordingSessions());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            new CreateUserRequest("new-user", "new-user@example.test", "New User", null), CancellationToken.None));

        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task AdministrativePasswordResetRevokesAllSessionsAndRequiresRotation()
    {
        await using var db = CreateSqliteDb();
        var user = new AppUser
        {
            UserName = "reset-user",
            Email = "reset-user@example.test",
            DisplayName = "Reset User",
            PasswordHash = new PasswordHasher<AppUser>().HashPassword(null!, "Old-password-2026!"),
            IsActive = true,
            MustChangePassword = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var sessions = new RecordingSessions();
        var service = new UsersService(db, new PasswordHasher<AppUser>(), sessions);

        Assert.True(await service.ResetPasswordAsync(user.Id, "New-password-2026!", CancellationToken.None));

        var saved = await db.Users.SingleAsync();
        Assert.True(saved.MustChangePassword);
        Assert.Equal(user.Id, sessions.RevokedUserId);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(saved, saved.PasswordHash, "New-password-2026!"));
    }

    [Fact]
    public async Task PublicRecoveryFailsClosedWithoutCreatingOrExposingAToken()
    {
        await using var db = CreateSqliteDb();
        var user = new AppUser { UserName = "recover-user", Email = "recover-user@example.test", DisplayName = "Recover User", PasswordHash = "hash", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new ProfileService(db, new PasswordHasher<AppUser>(), new RecordingSessions(), new UnavailablePasswordRecoveryDelivery(), null!);

        Assert.False(await service.RequestResetAsync(user.Email, CancellationToken.None));
        Assert.Equal(0, await db.PasswordResetTokens.CountAsync());
    }

    [Fact]
    public async Task AuthRateLimitRejectsTheSixthRequestFromOneIp()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(AuthRateLimiting.Partition);
        var context = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Loopback } };
        var leases = new List<RateLimitLease>();
        for (var index = 0; index < 6; index++) leases.Add(await limiter.AcquireAsync(context));

        Assert.Equal(5, leases.Count(lease => lease.IsAcquired));
        Assert.False(leases[^1].IsAcquired);
        foreach (var lease in leases) lease.Dispose();
    }

    private static EmgDbContext CreateSqliteDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseSqlite(connection).Options;
        var db = new EmgDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class RecordingSessions : IAuthSessionService
    {
        public int? RevokedUserId { get; private set; }
        public Task<(AuthSession Session, string Secret)> CreateAsync(AppUser user, bool rememberMe, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AppUser?> ValidateAsync(string secret, CancellationToken cancellationToken) => Task.FromResult<AppUser?>(null);
        public Task RevokeAsync(string secret, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RevokeAllAsync(int userId, CancellationToken cancellationToken) { RevokedUserId = userId; return Task.CompletedTask; }
    }
}
