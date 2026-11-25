using System.Security.Claims;
using EMG.Api.Modules.FinalAccounts.Dtos;
using EMG.Api.Modules.FinalAccounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.FinalAccounts.Controllers;

[ApiController]
[Authorize]
[PermissionAuthorize("final-accounts.view")]
[Route("api/final-accounts")]
public sealed class FinalAccountsController(IFinalAccountsService finalAccountsService) : ControllerBase
{
    [HttpGet("projects/{projectId:int}")]
    public async Task<ActionResult<FinalAccountSummary>> Get(int projectId, CancellationToken cancellationToken)
    {
        var result = await finalAccountsService.GetAsync(projectId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private bool HasGlobalProjectAccess() =>
        HttpContext?.User?.IsInRole("admin") == true ||
        HttpContext?.User?.IsInRole("director") == true ||
        HttpContext?.User?.IsInRole("accountant") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
