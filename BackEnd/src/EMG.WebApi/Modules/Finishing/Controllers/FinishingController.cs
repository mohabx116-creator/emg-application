using System.Security.Claims;
using EMG.Api.Modules.Finishing.Dtos;
using EMG.Api.Modules.Finishing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Finishing.Controllers;

[ApiController]
[Authorize]
[Route("api/finishing")]
public sealed class FinishingController(IFinishingService finishingService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("finishing.view")]
    public async Task<ActionResult<IReadOnlyList<FinishingSummary>>> Get(int? projectId, CancellationToken cancellationToken) =>
        Ok(await finishingService.GetAsync(projectId, CurrentUserId(), IsAdmin(), cancellationToken));

    [HttpPost]
    [PermissionAuthorize("finishing.create")]
    public async Task<ActionResult<FinishingSummary>> Create(CreateFinishingRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId <= 0 || request.Quantity <= 0 || request.UnitPrice < 0 || string.IsNullOrWhiteSpace(request.WorkType) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { message = "بيانات المشروع والعمل والوصف والكمية والسعر مطلوبة بشكل صحيح." });
        var result = await finishingService.CreateAsync(request, CurrentUserId(), IsAdmin(), cancellationToken);
        return result is null ? BadRequest(new { message = "بيانات التشطيبات غير صحيحة." }) : Ok(result);
    }

private bool IsAdmin() => HttpContext?.User?.IsInRole("admin") == true || HttpContext?.User?.IsInRole("director") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
