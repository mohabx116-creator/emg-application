using EMG.Api.Modules.ReportBuilder.Dtos;

namespace EMG.Api.Modules.ReportBuilder.Services;

public interface IReportBuilderService
{
    Task<ReportBuilderPreview> PreviewAsync(ReportBuilderQuery query, int? userId, bool isGlobalAccess, CancellationToken cancellationToken, bool includeProjectAmounts = false);
}
