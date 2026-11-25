using EMG.Api.Modules.Security.Dtos;

namespace EMG.Api.Modules.Security.Services;

public interface IAccountService
{
    Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
