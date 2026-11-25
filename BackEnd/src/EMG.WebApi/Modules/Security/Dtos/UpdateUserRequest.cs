namespace EMG.Api.Modules.Security.Dtos;

public sealed record UpdateUserRequest(
    string Email,
    string DisplayName,
    string? Role,
    bool IsActive,
    IReadOnlyList<string>? Roles = null);
