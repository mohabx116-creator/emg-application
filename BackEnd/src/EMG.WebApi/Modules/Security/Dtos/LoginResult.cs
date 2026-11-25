namespace EMG.Api.Modules.Security.Dtos;

public sealed record LoginResult(string SessionSecret, string Id, string Email, string Name, string Role, bool RememberMe);
