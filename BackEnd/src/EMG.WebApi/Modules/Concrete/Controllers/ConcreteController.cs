using System.Security.Claims;
using EMG.Api.Modules.Concrete.Dtos;
using EMG.Api.Modules.Concrete.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Concrete.Controllers;

[ApiController]
[Authorize]
[Route("api/concrete")]
public sealed class ConcreteController(IConcreteService concreteService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("concrete.view")]
    public async Task<ActionResult<IReadOnlyList<ConcreteSummary>>> Get(int? projectId, CancellationToken cancellationToken) =>
        Ok(await concreteService.GetAsync(projectId, CurrentUserId(), IsAdmin(), cancellationToken));

    [HttpPost]
    [PermissionAuthorize("concrete.create")]
    public async Task<ActionResult<ConcreteSummary>> Create(CreateConcreteRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId <= 0 || string.IsNullOrWhiteSpace(request.Description) || request.Quantity <= 0 || request.UnitPrice < 0)
            return BadRequest(new { message = "بيانات المشروع والوصف والكمية والسعر مطلوبة بشكل صحيح." });
        var result = await concreteService.CreateAsync(request, CurrentUserId(), IsAdmin(), cancellationToken);
        return result is null ? BadRequest(new { message = "نوع العنصر أو بيانات الخرسانة غير صحيحة." }) : Ok(result);
    }

private bool IsAdmin() => HttpContext?.User?.IsInRole("admin") == true || HttpContext?.User?.IsInRole("director") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
