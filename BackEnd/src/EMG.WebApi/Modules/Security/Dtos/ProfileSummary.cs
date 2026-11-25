namespace EMG.Api.Modules.Security.Dtos;

public sealed record ProfileSummary(int Id, string UserName, string Email, string DisplayName, string? ProfileImagePath, bool MustChangePassword);
