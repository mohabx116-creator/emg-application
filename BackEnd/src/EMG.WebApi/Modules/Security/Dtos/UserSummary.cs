namespace EMG.Api.Modules.Security.Dtos;

public sealed record UserSummary(
    int Id,
    string UserName,
    string Email,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> ProjectIds);
