using EMG.Api.Modules.Transactions.Dtos;

namespace EMG.Api.Modules.Transactions.Services;

public interface ITransactionCorrectionService
{
    Task<TransactionOperationResult?> CorrectAsync(string entityType, int entityId, TransactionCorrectionRequest request, int? userId, bool globalAccess, bool hasCorrectionPermission = false, CancellationToken cancellationToken = default);
    Task<TransactionOperationResult?> DeleteAsync(string entityType, int entityId, int? userId, bool globalAccess, bool hasDeletePermission = false, CancellationToken cancellationToken = default);
}
