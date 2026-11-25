namespace EMG.Api.Modules.Security.Dtos;

public sealed record CreateUserRequest(
    string UserName,
    string Email,
    string DisplayName,
    string? Password,
    string? Role = "user",
    IReadOnlyList<string>? Roles = null);
