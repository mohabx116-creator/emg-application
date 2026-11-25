namespace EMG.Api.Modules.Security.Dtos;

public sealed record ConfirmPasswordReset(string Email, string Token, string NewPassword);
