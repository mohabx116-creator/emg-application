using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EMG.Api.Modules.UniversalImports.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[PermissionAuthorize("data.migrate")]
[Route("api/historical-migration")]
public sealed class HistoricalBulkMigrationController(HistoricalBulkMigrationService service) : ControllerBase
{
    [HttpPost("run")]
    public async Task<ActionResult<HistoricalBulkMigrationResult>> Run(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Forbid();
        try { return Ok(await service.RunAsync(actor, cancellationToken)); }
        catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
    }
}
