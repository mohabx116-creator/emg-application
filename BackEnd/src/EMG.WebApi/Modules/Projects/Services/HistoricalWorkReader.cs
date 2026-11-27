using System.Linq.Expressions;
using EMG.Domain.Entities;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Projects.Services;

public sealed record HistoricalWorkDetail(long Id, int ProjectId, string ProjectName, string SourceFileName,
    string SourceSha256, string Sheet, string Cell, string Category, string Family, string Currency, decimal Amount,
    DateTime? WorkDate, string Description, string Unit, decimal? Quantity, decimal? Rate, decimal? Progress,
    string Party, int? SupplierId, int? ContractorId, string? OperationalTable, int? OperationalId);

public sealed record HistoricalSectionWorks(IReadOnlyList<HistoricalWorkDetail> Rows, IReadOnlyList<int> LinkedOperationalIds);

public static class HistoricalWorkReader
{
    public static readonly Expression<Func<HistoricalWorkLine, HistoricalWorkDetail>> Detail = w =>
        new HistoricalWorkDetail(w.Id, w.Batch.ProjectId, w.Batch.Project.Name, w.Batch.SourceFileName,
            w.Batch.SourceSha256, w.Sheet, w.Cell, w.Category, w.Family, w.Currency, w.Amount, w.WorkDate,
            w.Description, w.Unit, w.Quantity, w.Rate, w.Progress, w.Party, w.SupplierId, w.ContractorId,
            w.OperationalTable, w.OperationalId);

    public static async Task<HistoricalSectionWorks?> ReadSectionAsync(EmgDbContext db, int projectId, string section,
        int? userId, bool globalAccess, CancellationToken ct)
    {
        (string target, string[] families) = section switch
        {
            "movements" => ("SiteMovement", new[] { "site" }),
            "excavation" => ("ExcavationRecord", new[] { "excavation" }),
            "concrete" => ("ConcreteRecord", new[] { "concrete", "workmanship" }),
            "steel" => ("SteelRecord", new[] { "steel" }),
            "finishing" => ("FinishingRecord", new[] { "building", "finishing" }),
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };
        var projects = db.Projects.Where(p => p.Id == projectId && (globalAccess ||
            db.UserProjectAssignments.Any(a => a.ProjectId == p.Id && a.UserId == userId && a.IsActive)));
        if (!await projects.AnyAsync(ct)) return null;
        var source = db.HistoricalWorkLines.AsNoTracking().Where(w => projects.Any(p => p.Id == w.Batch.ProjectId));
        var rows = await source.Where(w => families.Contains(w.Family))
            .OrderBy(w => w.BatchId).ThenBy(w => w.Sheet).ThenBy(w => w.SourceRow).ThenBy(w => w.Cell)
            .Select(Detail).ToListAsync(ct);
        // Older postings sometimes used a different operational section from the original workbook.
        // Suppress their conventional representation by persisted target, independently of the original family.
        var linkedIds = await source.Where(w => w.OperationalTable == target && w.OperationalId != null)
            .Select(w => w.OperationalId!.Value).ToListAsync(ct);
        return new(rows, linkedIds);
    }

    public static Task<bool> IsLinkedAsync(EmgDbContext db, string transactionType, int id, CancellationToken ct)
    {
        var target = transactionType switch
        {
            "site-movement" => "SiteMovement",
            "excavation" => "ExcavationRecord",
            "concrete" => "ConcreteRecord",
            "steel" => "SteelRecord",
            "finishing" => "FinishingRecord",
            _ => null
        };
        return target is null ? Task.FromResult(false) :
            db.HistoricalWorkLines.AnyAsync(w => w.OperationalTable == target && w.OperationalId == id, ct);
    }
}