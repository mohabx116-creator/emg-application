using EMG.Api.Modules.ConsolidatedStatements.Dtos;

namespace EMG.Api.Modules.ConsolidatedStatements.Services;

public interface IConsolidatedStatementService
{
    Task<ConsolidatedStatementPreview> PreviewAsync(int? userId, bool isGlobalAccess, CancellationToken cancellationToken);
}
