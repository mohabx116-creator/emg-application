using System.Security.Claims;
using EMG.Api.Modules.ConsolidatedStatements.Dtos;
using EMG.Api.Modules.ConsolidatedStatements.Services;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.ConsolidatedStatements.Controllers;

[ApiController]
[Authorize]
[Route("api/reports/consolidated")]
public sealed class ConsolidatedStatementsController(IConsolidatedStatementService service) : ControllerBase
{
    [HttpGet("master-statement/preview")]
    [PermissionAuthorize("reports.view")]
    public async Task<ActionResult<ConsolidatedStatementPreview>> Preview(CancellationToken cancellationToken)
    {
        var result = await service.PreviewAsync(CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return Ok(result);
    }

    private bool HasGlobalProjectAccess() => User.IsInRole("admin") || User.IsInRole("director") || User.IsInRole("accountant");
    private int? CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
