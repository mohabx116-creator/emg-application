using System.Security.Claims;
using EMG.Api.Modules.AccountingClassification.Dtos;
using EMG.Api.Modules.AccountingClassification.Services;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.AccountingClassification.Controllers;

[ApiController]
[Authorize]
[Route("api/accounting-classification")]
public sealed class AccountingClassificationController(IAccountingClassificationService service) : ControllerBase
{
    [HttpGet("projects/{projectId:int}/preview")]
    [PermissionAuthorize("final-accounts.view")]
    public async Task<ActionResult<AccountingClassificationPreview>> Preview(int projectId, CancellationToken cancellationToken)
    {
        var result = await service.PreviewAsync(projectId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private bool HasGlobalProjectAccess() =>
        User.IsInRole("admin") || User.IsInRole("director") || User.IsInRole("accountant");

    private int? CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
