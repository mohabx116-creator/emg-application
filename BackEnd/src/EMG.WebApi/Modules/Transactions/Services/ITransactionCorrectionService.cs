using EMG.Api.Modules.Transactions.Dtos;

namespace EMG.Api.Modules.Transactions.Services;

public interface ITransactionCorrectionService
{
    Task<TransactionOperationResult?> CorrectAsync(string entityType, int entityId, TransactionCorrectionRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<TransactionOperationResult?> CancelAsync(string entityType, int entityId, TransactionCancellationRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
}
