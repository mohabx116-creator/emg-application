namespace EMG.Api.Modules.Security.Dtos;

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);
