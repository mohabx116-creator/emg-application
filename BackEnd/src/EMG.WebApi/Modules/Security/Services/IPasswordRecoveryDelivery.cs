namespace EMG.Api.Modules.Security.Services;

public interface IPasswordRecoveryDelivery
{
    Task<bool> DeliverAsync(string email, string token, CancellationToken cancellationToken);
}
