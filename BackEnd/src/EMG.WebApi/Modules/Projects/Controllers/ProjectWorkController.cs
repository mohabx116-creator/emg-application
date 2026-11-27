using System.Security.Claims;
using EMG.Api.Modules.Projects.Services;
using EMG.Api.Modules.Security.Authorization;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.Projects.Controllers;

[ApiController, Authorize, Route("api/projects/{projectId:int}/historical-work")]
public sealed class ProjectWorkController(EmgDbContext db) : ControllerBase
{
    [HttpGet("movements"), PermissionAuthorize("site-movements.view")]
    public Task<ActionResult<HistoricalSectionWorks>> Movements(int projectId, CancellationToken ct) => Read(projectId, "movements", ct);

    [HttpGet("excavation"), PermissionAuthorize("excavation.view")]
    public Task<ActionResult<HistoricalSectionWorks>> Excavation(int projectId, CancellationToken ct) => Read(projectId, "excavation", ct);

    [HttpGet("concrete"), PermissionAuthorize("concrete.view")]
    public Task<ActionResult<HistoricalSectionWorks>> Concrete(int projectId, CancellationToken ct) => Read(projectId, "concrete", ct);

    [HttpGet("steel"), PermissionAuthorize("steel.view")]
    public Task<ActionResult<HistoricalSectionWorks>> Steel(int projectId, CancellationToken ct) => Read(projectId, "steel", ct);

    [HttpGet("finishing"), PermissionAuthorize("finishing.view")]
    public Task<ActionResult<HistoricalSectionWorks>> Finishing(int projectId, CancellationToken ct) => Read(projectId, "finishing", ct);

    private async Task<ActionResult<HistoricalSectionWorks>> Read(int projectId, string section, CancellationToken ct)
    {
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var global = User.IsInRole("admin") || section != "movements" && User.IsInRole("director");
        var result = await HistoricalWorkReader.ReadSectionAsync(db, projectId, section, userId, global, ct);
        return result is null ? NotFound() : Ok(result);
    }
}