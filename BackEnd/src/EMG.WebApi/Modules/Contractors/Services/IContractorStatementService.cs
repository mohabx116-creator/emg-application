using EMG.Api.Modules.Contractors.Dtos;

namespace EMG.Api.Modules.Contractors.Services;

public interface IContractorStatementService
{
    Task<IReadOnlyList<ContractorSummary>> GetContractorsAsync(int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<ContractorSummary?> CreateContractorAsync(CreateContractorRequest request, CancellationToken cancellationToken);
    Task<ContractorContractSummary?> CreateContractAsync(int contractorId, CreateContractRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<ContractorContractItemSummary?> AddItemAsync(int contractId, CreateContractItemRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContractorContractSummary>> GetContractsAsync(int? projectId, int? contractorId, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContractorContractItemSummary>> GetItemsAsync(int contractId, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<IReadOnlyList<EligibleContractorWorkSummary>> GetEligibleWorkAsync(int contractId, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContractorStatementSummary>> GetStatementsAsync(int? projectId, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<ContractorStatementSummary?> CreateDraftAsync(CreateStatementRequest request, int? userId, bool globalAccess, CancellationToken cancellationToken);
    Task<byte[]?> ExportDraftAsync(int statementId, int? userId, bool globalAccess, CancellationToken cancellationToken);
}
