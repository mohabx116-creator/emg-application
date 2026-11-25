using System.Security.Claims;
using EMG.Api.Modules.Steel.Dtos;
using EMG.Api.Modules.Steel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Steel.Controllers;

[ApiController]
[Authorize]
[Route("api/steel")]
public sealed class SteelController(ISteelService steelService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("steel.view")]
    public async Task<ActionResult<IReadOnlyList<SteelSummary>>> Get(int? projectId, CancellationToken cancellationToken) =>
        Ok(await steelService.GetAsync(projectId, CurrentUserId(), IsAdmin(), cancellationToken));

    [HttpPost]
    [PermissionAuthorize("steel.create")]
    public async Task<ActionResult<SteelSummary>> Create(CreateSteelRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId <= 0 || request.Quantity <= 0 || request.UnitPrice < 0 || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { message = "بيانات المشروع والوصف والكمية والسعر مطلوبة بشكل صحيح." });
        var result = await steelService.CreateAsync(request, CurrentUserId(), IsAdmin(), cancellationToken);
        return result is null ? BadRequest(new { message = "بيانات الحديد غير صحيحة." }) : Ok(result);
    }

private bool IsAdmin() => HttpContext?.User?.IsInRole("admin") == true || HttpContext?.User?.IsInRole("director") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
