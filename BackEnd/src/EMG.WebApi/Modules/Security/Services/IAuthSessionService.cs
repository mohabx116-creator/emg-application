using EMG.Domain.Security;

namespace EMG.Api.Modules.Security.Services;

public interface IAuthSessionService
{
    Task<(AuthSession Session, string Secret)> CreateAsync(AppUser user, bool rememberMe, CancellationToken cancellationToken);
    Task<AppUser?> ValidateAsync(string secret, CancellationToken cancellationToken);
    Task RevokeAsync(string secret, CancellationToken cancellationToken);
    Task RevokeAllAsync(int userId, CancellationToken cancellationToken);
}
