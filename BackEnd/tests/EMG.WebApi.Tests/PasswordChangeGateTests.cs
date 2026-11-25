using System.Security.Claims;
using EMG.Api.Modules.Security;
using EMG.Api.Modules.Security.Dtos;
using EMG.Api.Modules.Security.Services;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EMG.WebApi.Tests;

public sealed class PasswordChangeGateTests
{
    [Fact]
    public async Task MustChangePasswordBlocksNormalProtectedRequests()
    {
        await using var db = CreateUserDbContext(mustChangePassword: true, out var userId);
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("must_change_password", "true")
            ], "test")));
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, db);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task PasswordChangeEndpointRemainsReachableBeforeRotation()
    {
        await using var db = CreateUserDbContext(mustChangePassword: true, out var userId);
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("must_change_password", "true")
            ], "test")));
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowWhenPasswordChangeRequiredAttribute()),
            "change-password"));
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, db);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task RotatedPasswordTokenCanReachNormalProtectedRequests()
    {
        await using var db = CreateUserDbContext(mustChangePassword: false, out var userId);
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("must_change_password", "false")
            ], "test")));
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, db);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task LoginCreatesAnOpaqueSessionWithoutMintingAJwt()
    {
        await using var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new AppUser
        {
            UserName = "rotation-admin@example.test",
            Email = "rotation-admin@example.test",
            DisplayName = "Rotation Administrator",
            MustChangePassword = true
        };
        var password = $"Test-{Guid.NewGuid():N}-Strong!";
        user.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>()
            .HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new AccountService(db, new Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>(), new FakeAuthSessionService());
        var result = await service.LoginAsync(new LoginRequest(user.Email, password), CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.SessionSecret));
        Assert.True(result.RememberMe == false);
    }

    [Fact]
    public async Task TokenMintedBeforeResetIsBlockedByCurrentDatabaseState()
    {
        await using var db = CreateUserDbContext(mustChangePassword: false, out var userId);
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("must_change_password", "false")
            ], "test")));
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var user = await db.Users.SingleAsync();
        user.MustChangePassword = true;
        await db.SaveChangesAsync();

        await middleware.InvokeAsync(context, db);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task TokenWithoutRotationClaimIsBlockedWhenDatabaseRequiresRotation()
    {
        await using var db = CreateUserDbContext(mustChangePassword: true, out var userId);
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")));
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, db);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task MeReturnsCurrentDatabaseRotationStateAlongsideExistingAccountFields()
    {
        await using var db = CreateUserDbContext(mustChangePassword: true, out var userId);
        var controller = new EMG.Api.Modules.Security.Controllers.AccountController(
            null!, null!, new FakeAuthSessionService(), Configuration(), db, null!);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, "gate-user@example.test"),
                new Claim(ClaimTypes.Name, "Gate User")
            ], "test"));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var result = await controller.Me(CancellationToken.None);
        var payload = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result).Value;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var user = json.RootElement.GetProperty("user");

        Assert.True(user.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal("gate-user@example.test", user.GetProperty("email").GetString());
        Assert.True(user.TryGetProperty("roles", out _));
        Assert.True(user.TryGetProperty("permissions", out _));
    }

    private static DefaultHttpContext CreateContext(ClaimsPrincipal user) => new()
    {
        User = user,
        Response = { Body = new MemoryStream() }
    };

    private static EmgDbContext CreateUserDbContext(bool mustChangePassword, out int userId)
    {
        var db = new EmgDbContext(new DbContextOptionsBuilder<EmgDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new AppUser
        {
            UserName = "gate-user@example.test",
            Email = "gate-user@example.test",
            DisplayName = "Gate User",
            PasswordHash = "hash",
            MustChangePassword = mustChangePassword
        };
        db.Users.Add(user);
        db.SaveChanges();
        userId = user.Id;
        return db;
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-key-that-is-long-enough-for-hmac-sha256-2026",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience"
        })
        .Build();

    private sealed class FakeAuthSessionService : IAuthSessionService
    {
        public Task<(AuthSession Session, string Secret)> CreateAsync(AppUser user, bool rememberMe, CancellationToken cancellationToken) =>
            Task.FromResult((new AuthSession { UserId = user.Id, RememberMe = rememberMe, ExpiresAtUtc = DateTime.UtcNow.AddHours(1) }, "test-session-secret"));

        public Task<AppUser?> ValidateAsync(string secret, CancellationToken cancellationToken) => Task.FromResult<AppUser?>(null);
        public Task RevokeAsync(string secret, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RevokeAllAsync(int userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
