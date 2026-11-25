using System.Security.Cryptography;
using System.Text;
using EMG.Api.Modules.Security.Dtos;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Security.Services;

public sealed class ProfileService(EmgDbContext db, IPasswordHasher<AppUser> passwordHasher, IAuthSessionService sessions, IPasswordRecoveryDelivery recoveryDelivery, IWebHostEnvironment environment) : IProfileService
{
    public async Task<ProfileSummary?> GetAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null ? null : ToSummary(user);
    }

    public async Task<ProfileSummary?> UpdateAsync(int userId, string displayName, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(displayName)) return null;
        user.DisplayName = displayName.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(user);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8 || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RequestResetAsync(string email, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == email.Trim() && item.IsActive, cancellationToken);
        if (user is null) return false;
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        if (!await recoveryDelivery.DeliverAsync(user.Email, rawToken, cancellationToken)) return false;
        db.PasswordResetTokens.Add(new PasswordResetToken { UserId = user.Id, TokenHash = Hash(rawToken), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30) });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ConfirmResetAsync(string email, string token, string newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8) return false;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == email.Trim() && item.IsActive, cancellationToken);
        if (user is null) return false;
        var tokenHash = Hash(token);
        var reset = await db.PasswordResetTokens.Where(item => item.UserId == user.Id && item.TokenHash == tokenHash && item.UsedAtUtc == null && item.ExpiresAtUtc > DateTime.UtcNow).OrderByDescending(item => item.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (reset is null) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        reset.UsedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ProfileSummary?> UploadImageAsync(int userId, IFormFile file, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (user is null || file.Length <= 0 || file.Length > 5 * 1024 * 1024 || extension is not (".jpg" or ".jpeg" or ".png" or ".webp") || !await HasExpectedImageSignatureAsync(file, extension, cancellationToken)) return null;
        var folder = Path.Combine(environment.ContentRootPath, "uploads", "profiles", userId.ToString());
        Directory.CreateDirectory(folder);
        var storedName = $"profile-{Guid.NewGuid():N}{extension}";
        await using (var stream = File.Create(Path.Combine(folder, storedName))) await file.CopyToAsync(stream, cancellationToken);
        user.ProfileImagePath = Path.Combine("uploads", "profiles", userId.ToString(), storedName).Replace('\\', '/');
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(user);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static ProfileSummary ToSummary(AppUser item) => new(
        item.Id,
        item.UserName,
        item.Email,
        item.DisplayName,
        item.ProfileImagePath is null ? null : $"/profile-images/{item.Id}/{Uri.EscapeDataString(Path.GetFileName(item.ProfileImagePath))}",
        item.MustChangePassword);

    private static async Task<bool> HasExpectedImageSignatureAsync(IFormFile file, string extension, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(), cancellationToken);
        return extension switch
        {
            ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => read >= 8 && Matches(header, 0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            ".webp" => read >= 12 && Matches(header, 0, "RIFF"u8.ToArray()) && Matches(header, 8, "WEBP"u8.ToArray()),
            _ => false
        };
    }

    private static bool Matches(byte[] source, int offset, byte[] expected)
    {
        if (source.Length < offset + expected.Length) return false;
        for (var index = 0; index < expected.Length; index++)
            if (source[offset + index] != expected[index]) return false;
        return true;
    }
}
