using EMG.Api.Modules.FinalAccounts.Dtos;

namespace EMG.Api.Modules.FinalAccounts.Services;

public interface IFinalAccountsService
{
    Task<FinalAccountSummary?> GetAsync(int projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
