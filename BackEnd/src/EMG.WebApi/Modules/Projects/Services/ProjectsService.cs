using EMG.Api.Modules.Projects.Dtos;
using EMG.Api.Modules.Projects.Repositories;
using EMG.Api.Modules.FinalAccounts.Services;

namespace EMG.Api.Modules.Projects.Services;

public sealed class ProjectsService(IProjectsRepository repository, IFinalAccountsService? finalAccountsService = null) : IProjectsService
{
    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(int? userId, bool isAdmin, CancellationToken cancellationToken) =>
        await AddFinancialSummariesAsync(await repository.GetProjectsAsync(userId, isAdmin, cancellationToken), userId, isAdmin, cancellationToken);

    public async Task<ProjectSummary?> GetProjectAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var project = await repository.GetProjectAsync(id, userId, isAdmin, cancellationToken);
        if (project is null || finalAccountsService is null) return project;
        return await AddFinancialSummaryAsync(project, userId, isAdmin, cancellationToken);
    }

    public Task<ProjectSummary> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken) => repository.CreateAsync(request, cancellationToken);

    public Task<ProjectSummary?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken) => repository.UpdateAsync(id, request, cancellationToken);

    public Task<ProjectSummary?> DecideImportAsync(int id, bool approve, string? notes, int? userId, CancellationToken cancellationToken) => repository.DecideImportAsync(id, approve, notes, userId, cancellationToken);

    private async Task<IReadOnlyList<ProjectSummary>> AddFinancialSummariesAsync(IReadOnlyList<ProjectSummary> projects, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (finalAccountsService is null || projects.Count == 0) return projects;
        var enriched = new List<ProjectSummary>(projects.Count);
        foreach (var project in projects)
        {
            enriched.Add(await AddFinancialSummaryAsync(project, userId, isAdmin, cancellationToken));
        }

        return enriched;
    }

    private async Task<ProjectSummary> AddFinancialSummaryAsync(ProjectSummary project, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var account = await finalAccountsService!.GetAsync(project.Id, userId, isAdmin, cancellationToken);
        return account is null ? project : project with
        {
            TotalWorks = account.TotalWorks,
            TotalPayments = account.TotalPayments,
            CashBalance = account.CashBalance,
            CashBalanceStatus = account.CashBalanceStatus,
            Sections = account.Sections,
            HistoricalFundingBasis = account.HistoricalFundingBasis,
            FinancialWarnings = account.FinancialWarnings,
            HistoricalFundingUnknown = account.HistoricalFundingUnknown,
            TotalIssued = account.TotalWorks,
            TotalReceived = account.TotalIncoming
        };
    }
}
