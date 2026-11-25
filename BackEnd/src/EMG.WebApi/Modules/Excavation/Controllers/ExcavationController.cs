using System.Security.Claims;
using EMG.Api.Modules.Excavation.Dtos;
using EMG.Api.Modules.Excavation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Excavation.Controllers;

[ApiController]
[Authorize]
[Route("api/excavation")]
public sealed class ExcavationController(IExcavationService excavationService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("excavation.view")]
    public async Task<ActionResult<IReadOnlyList<ExcavationSummary>>> Get(int? projectId, CancellationToken cancellationToken) =>
        Ok(await excavationService.GetAsync(projectId, CurrentUserId(), IsAdmin(), cancellationToken));

    [HttpPost]
    [PermissionAuthorize("excavation.create")]
    public async Task<ActionResult<ExcavationSummary>> Create(CreateExcavationRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId <= 0 || string.IsNullOrWhiteSpace(request.Description) || request.UnitPrice < 0)
            return BadRequest(new { message = "بيانات المشروع والوصف والسعر مطلوبة بشكل صحيح." });
        var result = await excavationService.CreateAsync(request, CurrentUserId(), IsAdmin(), cancellationToken);
        return result is null ? BadRequest(new { message = "طريقة الحساب أو أبعاد الحفر غير صحيحة." }) : Ok(result);
    }

private bool IsAdmin() => HttpContext?.User?.IsInRole("admin") == true || HttpContext?.User?.IsInRole("director") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
