using EMG.Api.Modules.Search.Dtos;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Search.Services;

public sealed class SearchService(EmgDbContext db) : ISearchService
{
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int? userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var term = query.Trim();
        if (term.Length < 2) return [];
        var results = new List<SearchResult>();

        results.AddRange(await db.Projects.AsNoTracking().Where(item =>
                item.Name.Contains(term) || item.Code.Contains(term) || (item.ClientName ?? string.Empty).Contains(term) || (item.Location ?? string.Empty).Contains(term))
            .OrderBy(item => item.Name).Take(15).Select(item => new SearchResult("project", item.Id, item.Name, item.Code, item.Id, item.Status)).ToListAsync(cancellationToken));

        results.AddRange(await db.Suppliers.AsNoTracking().Where(item =>
                item.Name.Contains(term) || (item.Phone ?? string.Empty).Contains(term) || (item.TaxNumber ?? string.Empty).Contains(term))
            .OrderBy(item => item.Name).Take(15).Select(item => new SearchResult("supplier", item.Id, item.Name, item.SupplierType, null, item.IsActive ? "نشط" : "غير نشط")).ToListAsync(cancellationToken));

        var projectIds = await AccessibleProjectIdsAsync(userId, isAdmin, cancellationToken);
        if (!isAdmin && projectIds.Count == 0) return results;

        results.AddRange(await db.SiteMovements.AsNoTracking().Where(item =>
                (isAdmin || projectIds.Contains(item.ProjectId)) && (item.Description.Contains(term) || (item.Reference ?? string.Empty).Contains(term) || (item.Details ?? string.Empty).Contains(term)))
            .OrderByDescending(item => item.MovementDate).Take(15).Select(item => new SearchResult("site-movement", item.Id, item.Description, item.MovementType, item.ProjectId, item.Status)).ToListAsync(cancellationToken));

        results.AddRange(await db.AdvancePayments.AsNoTracking().Where(item =>
                (isAdmin || projectIds.Contains(item.ProjectId)) && (item.ClientName.Contains(term) || item.PayerName.Contains(term) || (item.Reference ?? string.Empty).Contains(term)))
            .OrderByDescending(item => item.PaymentDate).Take(15).Select(item => new SearchResult("advance-payment", item.Id, item.PayerName, item.ClientName, item.ProjectId, item.Status)).ToListAsync(cancellationToken));

        results.AddRange(await db.ExcavationRecords.AsNoTracking().Where(item =>
                (isAdmin || projectIds.Contains(item.ProjectId)) && (item.Description.Contains(term) || (item.SoilType ?? string.Empty).Contains(term)))
            .OrderByDescending(item => item.ExcavationDate).Take(10).Select(item => new SearchResult("excavation", item.Id, item.Description, item.CalculationMethod, item.ProjectId, item.Status)).ToListAsync(cancellationToken));

        results.AddRange(await db.ConcreteRecords.AsNoTracking().Where(item =>
                (isAdmin || projectIds.Contains(item.ProjectId)) && (item.Description.Contains(term) || item.ElementType.Contains(term) || (item.MixGrade ?? string.Empty).Contains(term)))
            .OrderByDescending(item => item.PourDate).Take(10).Select(item => new SearchResult("concrete", item.Id, item.Description, item.ElementType, item.ProjectId, item.Status)).ToListAsync(cancellationToken));

        results.AddRange(await db.SteelRecords.AsNoTracking().Where(item =>
                (isAdmin || projectIds.Contains(item.ProjectId)) && (item.Description.Contains(term) || (item.SteelType ?? string.Empty).Contains(term) || (item.BarDiameter ?? string.Empty).Contains(term)))
            .OrderByDescending(item => item.OperationDate).Take(10).Select(item => new SearchResult("steel", item.Id, item.Description, item.OperationType, item.ProjectId, item.Status)).ToListAsync(cancellationToken));

        results.AddRange(await db.FinishingRecords.AsNoTracking().Where(item =>
                (isAdmin || projectIds.Contains(item.ProjectId)) && (item.Description.Contains(term) || item.WorkType.Contains(term) || (item.AreaName ?? string.Empty).Contains(term)))
            .OrderByDescending(item => item.WorkDate).Take(10).Select(item => new SearchResult("finishing", item.Id, item.Description, item.WorkType, item.ProjectId, item.Status)).ToListAsync(cancellationToken));

        return results.Take(60).ToArray();
    }

    private async Task<List<int>> AccessibleProjectIdsAsync(int? userId, bool isAdmin, CancellationToken cancellationToken) =>
        isAdmin ? await db.Projects.Select(item => item.Id).ToListAsync(cancellationToken) : userId is null ? [] : await db.UserProjectAssignments.Where(item => item.UserId == userId && item.IsActive).Select(item => item.ProjectId).ToListAsync(cancellationToken);
}
