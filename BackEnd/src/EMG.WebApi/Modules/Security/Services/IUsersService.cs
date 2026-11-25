using EMG.Api.Modules.Security.Dtos;

namespace EMG.Api.Modules.Security.Services;

public interface IUsersService
{
    Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken);
    Task<UserSummary> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserSummary?> UpdateAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<bool> AssignProjectAsync(int userId, int projectId, CancellationToken cancellationToken);
    Task<bool> UnassignProjectAsync(int userId, int projectId, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(int userId, string? password, CancellationToken cancellationToken);
}
