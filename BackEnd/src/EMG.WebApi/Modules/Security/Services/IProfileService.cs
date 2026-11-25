using EMG.Api.Modules.Security.Dtos;
using Microsoft.AspNetCore.Http;

namespace EMG.Api.Modules.Security.Services;

public interface IProfileService
{
    Task<ProfileSummary?> GetAsync(int userId, CancellationToken cancellationToken);
    Task<ProfileSummary?> UpdateAsync(int userId, string displayName, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
    Task<bool> RequestResetAsync(string email, CancellationToken cancellationToken);
    Task<bool> ConfirmResetAsync(string email, string token, string newPassword, CancellationToken cancellationToken);
    Task<ProfileSummary?> UploadImageAsync(int userId, IFormFile file, CancellationToken cancellationToken);
}
