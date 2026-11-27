using EMG.Api.Modules.Advances.Dtos;

namespace EMG.Api.Modules.Advances.Services;

public interface IAdvancesService
{
    Task<AdvanceLedger?> GetLedgerAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdvanceSummary>> GetAsync(int? projectId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<AdvanceSummary?> CreateAsync(CreateAdvanceRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<IReadOnlyList<AllocationSummary>> GetAllocationsAsync(int advanceId, int? userId, bool isAdmin, CancellationToken cancellationToken);
    Task<AllocationSummary?> AllocateAsync(int advanceId, AllocateAdvanceRequest request, int? userId, bool isAdmin, CancellationToken cancellationToken);
}
