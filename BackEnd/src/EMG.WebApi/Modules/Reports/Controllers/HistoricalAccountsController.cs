using System.Security.Claims;
using EMG.Api.Modules.Projects.Services;
using EMG.Api.Modules.Security.Authorization;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Reports.Controllers;

[ApiController, Authorize, PermissionAuthorize("suppliers.view")]
public sealed class HistoricalAccountsController(EmgDbContext db) : ControllerBase
{
    [HttpGet("api/suppliers/{id:int}/historical-account")]
    public Task<IActionResult> Supplier(int id, CancellationToken ct) => Read(id, true, ct);

    [HttpGet("api/contractors/{id:int}/historical-account")]
    public Task<IActionResult> Contractor(int id, CancellationToken ct) => Read(id, false, ct);

    private async Task<IActionResult> Read(int id, bool supplier, CancellationToken ct)
    {
        var global = User.IsInRole("admin") || User.IsInRole("director") || User.IsInRole("accountant");
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var number) ? number : null;
        var allowed = db.UserProjectAssignments.Where(a => a.IsActive && a.UserId == userId).Select(a => a.ProjectId);
        var works = await db.HistoricalWorkLines.AsNoTracking()
            .Where(w => (supplier ? w.SupplierId == id : w.ContractorId == id) && (global || allowed.Contains(w.Batch.ProjectId)))
            .OrderBy(w => w.Batch.ProjectId).ThenBy(w => w.Sheet).ThenBy(w => w.SourceRow)
            .Select(HistoricalWorkReader.Detail).ToListAsync(ct);
        var settlements = await db.HistoricalSettlements.AsNoTracking()
            .Where(s => (supplier ? s.SupplierId == id : s.ContractorId == id) && (global || allowed.Contains(s.Batch.ProjectId)))
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Batch.ProjectId, ProjectName = s.Batch.Project.Name, s.Batch.SourceFileName,
                s.Batch.SourceSha256, s.Sheet, s.Cell, s.Category, s.Currency, s.Amount, s.SettlementDate, s.Narrative, s.Party })
            .ToListAsync(ct);
        if (works.Count == 0 && settlements.Count == 0) return NotFound();
        var totals = works.Select(w => (w.ProjectId, w.Currency)).Union(settlements.Select(s => (s.ProjectId,s.Currency)))
            .Select(key =>
            {
                var gross = works.Where(w => (w.ProjectId,w.Currency) == key).Sum(w => w.Amount);
                var paid = settlements.Where(s => (s.ProjectId,s.Currency) == key).Sum(s => s.Amount);
                return new { key.ProjectId, key.Currency, Works = gross, IdentifiedSettlements = paid, RecordedBalance = gross - paid };
            });
        return Ok(new { Works = works, Settlements = settlements, Totals = totals });
    }
}
