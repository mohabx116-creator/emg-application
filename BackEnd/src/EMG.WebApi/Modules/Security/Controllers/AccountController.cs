using EMG.Api.Modules.Security.Dtos;
using EMG.Api.Modules.Security.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using EMG.Api.Modules.Security.Authentication;

namespace EMG.Api.Modules.Security.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController(IAccountService accountService, IProfileService profileService, IAuthSessionService sessions, IConfiguration configuration, EmgDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [AllowWhenPasswordChangeRequired]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.LoginAsync(request, cancellationToken);
        if (result is null) return Unauthorized(new { message = "بيانات الدخول غير صحيحة أو الحساب غير نشط." });
        Response.Cookies.Append(SessionAuthenticationHandler.CookieName, result.SessionSecret, new CookieOptions
        {
            HttpOnly = true, Secure = !IsDevelopment(), SameSite = SameSiteMode.Lax, Path = "/",
            MaxAge = result.RememberMe ? TimeSpan.FromDays(30) : null
        });
        return Ok(new { user = new { id = result.Id, email = result.Email, name = result.Name, role = result.Role } });
    }

    [Authorize, AllowWhenPasswordChangeRequired, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(SessionAuthenticationHandler.CookieName, out var secret)) await sessions.RevokeAsync(secret, cancellationToken);
        Response.Cookies.Delete(SessionAuthenticationHandler.CookieName);
        return NoContent();
    }

    [AllowAnonymous, AllowWhenPasswordChangeRequired, HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        Response.Cookies.Append("emg_csrf", token, new CookieOptions { HttpOnly = false, Secure = !IsDevelopment(), SameSite = SameSiteMode.Lax, Path = "/" });
        return Ok(new { csrf = true });
    }

    [Authorize]
    [AllowWhenPasswordChangeRequired]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var permissions = await db.UserRoles
            .Where(item => item.UserId == userId)
            .SelectMany(item => item.Role.RolePermissions.Select(link => link.Permission.Code))
            .Distinct()
            .ToListAsync(cancellationToken);
        var roles = await db.UserRoles
            .Where(item => item.UserId == userId)
            .Select(item => item.Role.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
        var projectIds = await db.UserProjectAssignments
            .Where(item => item.UserId == userId && item.IsActive)
            .Select(item => item.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var mustChangePassword = await db.Users
            .Where(item => item.Id == userId)
            .Select(item => (bool?)item.MustChangePassword)
            .SingleOrDefaultAsync(cancellationToken) ?? true;

        return Ok(new
        {
            user = new
            {
                id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                name = User.Identity?.Name,
                email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value,
                role = roles.FirstOrDefault() ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                roles,
                projectIds,
                permissions,
                mustChangePassword
            }
        });
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken) =>
        (await profileService.GetAsync(CurrentUserId(), cancellationToken)) is { } profile ? Ok(profile) : NotFound();

    [Authorize]
    [HttpGet("profile-image")]
    public async Task<IActionResult> ProfileImage(CancellationToken cancellationToken)
    {
        var relativePath = await db.Users.Where(user => user.Id == CurrentUserId()).Select(user => user.ProfileImagePath).SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(relativePath)) return NotFound();
        var profilesRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "uploads", "profiles", CurrentUserId().ToString()));
        var fullPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(profilesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath)) return NotFound();
        return PhysicalFile(fullPath, GetImageContentType(Path.GetExtension(fullPath)));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        (await profileService.UpdateAsync(CurrentUserId(), request.DisplayName, cancellationToken)) is { } profile ? Ok(profile) : BadRequest();

    [Authorize]
    [AllowWhenPasswordChangeRequired]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        await profileService.ChangePasswordAsync(CurrentUserId(), request.CurrentPassword, request.NewPassword, cancellationToken)
            ? Ok(new { message = "تم تغيير كلمة المرور." })
            : BadRequest(new { message = "كلمة المرور الحالية غير صحيحة أو كلمة المرور الجديدة غير صالحة." });

    [Authorize]
    [HttpPost("profile-image")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadProfileImage(IFormFile file, CancellationToken cancellationToken) =>
        file is null ? BadRequest(new { message = "الصورة مطلوبة." }) : (await profileService.UploadImageAsync(CurrentUserId(), file, cancellationToken)) is { } profile ? Ok(profile) : BadRequest(new { message = "الصورة غير صالحة أو أكبر من 5MB." });

    [AllowAnonymous]
    [AllowWhenPasswordChangeRequired]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("password-reset/request")]
    public async Task<IActionResult> RequestPasswordReset(RequestPasswordReset request, CancellationToken cancellationToken)
    {
        var delivered = await profileService.RequestResetAsync(request.Email, cancellationToken);
        return delivered
            ? Accepted(new { message = "إذا كان الحساب موجودًا، فسيتم إرسال تعليمات الاسترداد." })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = "RECOVERY_UNAVAILABLE",
                message = "خدمة استرداد كلمة المرور غير مفعلة حاليًا. تواصل مع مسؤول النظام."
            });
    }

    [AllowAnonymous]
    [AllowWhenPasswordChangeRequired]
    [EnableRateLimiting(AuthRateLimiting.PolicyName)]
    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(ConfirmPasswordReset request, CancellationToken cancellationToken) =>
        await profileService.ConfirmResetAsync(request.Email, request.Token, request.NewPassword, cancellationToken)
            ? Ok(new { message = "تم إعادة تعيين كلمة المرور." })
            : BadRequest(new { message = "رمز الاسترداد غير صالح أو منتهي." });

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private static string GetImageContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "image/png"
    };
    private bool IsDevelopment() => string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase) || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
}
