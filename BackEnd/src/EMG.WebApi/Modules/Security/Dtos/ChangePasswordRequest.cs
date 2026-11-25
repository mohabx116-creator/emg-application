namespace EMG.Api.Modules.Security.Dtos;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
