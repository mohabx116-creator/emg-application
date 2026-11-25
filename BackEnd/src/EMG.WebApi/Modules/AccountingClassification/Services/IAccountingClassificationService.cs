using EMG.Api.Modules.AccountingClassification.Dtos;

namespace EMG.Api.Modules.AccountingClassification.Services;

public interface IAccountingClassificationService
{
    Task<AccountingClassificationPreview?> PreviewAsync(int projectId, int? userId, bool isGlobalAccess, CancellationToken cancellationToken);
}
