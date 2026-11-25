namespace EMG.Api.Modules.Security.Services;

/// <summary>
/// Safe default until an approved email/SMS provider is configured.
/// It never exposes or logs the recovery token.
/// </summary>
public sealed class UnavailablePasswordRecoveryDelivery : IPasswordRecoveryDelivery
{
    public Task<bool> DeliverAsync(string email, string token, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
