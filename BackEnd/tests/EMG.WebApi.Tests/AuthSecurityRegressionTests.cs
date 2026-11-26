using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using EMG.Api.Modules.Security;
using EMG.Api.Modules.Security.Controllers;
using EMG.Api.Modules.Security.Dtos;
using EMG.Api.Modules.Security.Services;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EMG.WebApi.Tests;

public sealed class AuthSecurityRegressionTests
{
    [Fact]
    public async Task DelegatedUserManagementRejectsAdminAndHigherLimits()
    {
        await using var db = CreateSqliteDb();
        var caller = new AppUser { UserName = "delegate", Email = "delegate@example.test", IsActive = true };
        var admin = new AppUser { UserName = "admin-qa", Email = "admin@example.test", IsActive = true };
        var delegatedRole = new Role { Name = "delegated" };
        var adminRole = new Role { Name = "admin" };
        var higherRole = new Role { Name = "higher" };
        var permission = new Permission { Code = "qa.amount" };
        db.UserRoles.AddRange(new UserRole { User = caller, Role = delegatedRole }, new UserRole { User = admin, Role = adminRole });
        db.RolePermissions.AddRange(
            new RolePermission { Role = delegatedRole, Permission = permission, Scope = "assigned", AmountLimit = 100 },
            new RolePermission { Role = higherRole, Permission = permission, Scope = "assigned", AmountLimit = 200 });
        await db.SaveChangesAsync();
        var controller = new UsersController(new UsersService(db, new PasswordHasher<AppUser>(), new AuthSessionService(db)), db)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, caller.Id.ToString())], "qa"))
            } }
        };
        var create = new CreateUserRequest("new", "new@example.test", "New", "Test-password-2026!", "admin");
        Assert.IsType<ForbidResult>((await controller.CreateUser(create, default)).Result);
        Assert.IsType<ForbidResult>((await controller.CreateUser(create with { Role = "higher" }, default)).Result);
        Assert.IsType<ForbidResult>((await controller.UpdateUser(admin.Id, new(admin.Email, "Renamed", "delegated", true), default)).Result);
        Assert.IsType<ForbidResult>(await controller.ResetPassword(admin.Id, new("Test-password-2026!"), default));
        Assert.IsType<OkObjectResult>((await controller.CreateUser(create with { Role = "delegated" }, default)).Result);
    }

    [Fact]
    public async Task ReactivationDoesNotRestoreARevokedSession()
    {
        await using var db = CreateSqliteDb();
        var hasher = new PasswordHasher<AppUser>();
        var sessions = new AuthSessionService(db);
        var users = new UsersService(db, hasher, sessions);
        var user = await users.CreateAsync(new("session-user", "session@example.test", "Session", "Test-password-2026!"), default);
        var account = new AccountService(db, hasher, sessions);
        var login = await account.LoginAsync(new("session@example.test", "Test-password-2026!"), default);
        Assert.NotNull(login);
        await users.UpdateAsync(user.Id, new("session@example.test", "Session", "user", false), default);
        Assert.Null(await account.LoginAsync(new("session@example.test", "Test-password-2026!"), default));
        await users.UpdateAsync(user.Id, new("session@example.test", "Session", "user", true), default);
        Assert.Null(await sessions.ValidateAsync(login.SessionSecret, default));
        Assert.NotNull(await account.LoginAsync(new("session@example.test", "Test-password-2026!"), default));
    }

    [Fact]
    public async Task DeleteUnusedAccountPreservesBusinessAttributionAndAuditsDeletion()
    {
        await using var db = CreateSqliteDb();
        var hasher = new PasswordHasher<AppUser>();
        var users = new UsersService(db, hasher, new AuthSessionService(db));
        var admin = new AppUser { UserName = "actor", Email = "actor@example.test", IsActive = true };
        var unused = new AppUser { UserName = "unused", Email = "unused@example.test", IsActive = false };
        var referenced = new AppUser { UserName = "referenced", Email = "referenced@example.test", IsActive = false };
        var project = new EMG.Domain.Entities.Project { Code = "ACCOUNT-QA", Name = "QA" };
        db.AddRange(admin, unused, referenced, project);
        await db.SaveChangesAsync();
        db.SiteMovements.Add(new EMG.Domain.Entities.SiteMovement { ProjectId = project.Id, CreatedByUserId = referenced.Id });
        await db.SaveChangesAsync();

        Assert.False(await users.DeleteUnusedAsync(admin.Id, admin.Id, default));
        Assert.False(await users.DeleteUnusedAsync(referenced.Id, admin.Id, default));
        Assert.True(await users.DeleteUnusedAsync(unused.Id, admin.Id, default));
        Assert.False(await db.Users.AnyAsync(item => item.Id == unused.Id));
        Assert.Equal(referenced.Id, (await db.SiteMovements.SingleAsync()).CreatedByUserId);
        var audit = await db.SecurityAuditLogs.SingleAsync();
        Assert.Equal(admin.Id, audit.UserId);
        Assert.Equal(unused.Id, audit.EntityId);
    }

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
    public async Task LoginAcceptsOnlyTheCurrentPasswordAfterResetAndSelfServiceChange()
    {
        await using var db = CreateSqliteDb();
        var hasher = new PasswordHasher<AppUser>();
        var sessions = new AuthSessionService(db);
        var user = new AppUser
        {
            UserName = "rotation-user", Email = "rotation@example.test", DisplayName = "Rotation QA", IsActive = true
        };
        user.PasswordHash = hasher.HashPassword(user, "Original-password-2026!");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var users = new UsersService(db, hasher, sessions);
        var profile = new ProfileService(db, hasher, sessions, new UnavailablePasswordRecoveryDelivery(), null!);
        var accounts = new AccountService(db, hasher, sessions);

        Assert.True(await users.ResetPasswordAsync(user.Id, "Temporary-password-2026!", default));
        Assert.Null(await accounts.LoginAsync(new(user.Email, "Original-password-2026!"), default));
        var temporarySession = await accounts.LoginAsync(new(user.Email, "Temporary-password-2026!"), default);
        Assert.NotNull(temporarySession);
        Assert.True(await profile.ChangePasswordAsync(user.Id, "Temporary-password-2026!", "Final-password-2026!", default));
        Assert.False(user.MustChangePassword);
        Assert.Null(await sessions.ValidateAsync(temporarySession.SessionSecret, default));
        Assert.Null(await accounts.LoginAsync(new(user.Email, "Temporary-password-2026!"), default));
        Assert.NotNull(await accounts.LoginAsync(new(user.Email, "Final-password-2026!"), default));
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
