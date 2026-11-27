using System.Security.Claims;
using EMG.Api.Modules.Advances.Dtos;
using EMG.Api.Modules.Advances.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Advances.Controllers;

[ApiController]
[Authorize]
[Route("api/advances")]
public sealed class AdvancesController(IAdvancesService advancesService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("advances.view")]
    public async Task<ActionResult<IReadOnlyList<AdvanceSummary>>> Get(int? projectId, CancellationToken cancellationToken) =>
        Ok(await advancesService.GetAsync(projectId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken));

    [HttpGet("ledger")]
    [PermissionAuthorize("advances.view")]
    public async Task<ActionResult<AdvanceLedger>> Ledger(int? projectId, CancellationToken cancellationToken)
    {
        var ledger = await advancesService.GetLedgerAsync(projectId,CurrentUserId(),HasGlobalProjectAccess(),cancellationToken);
        return ledger is null ? NotFound() : Ok(ledger);
    }

    [HttpPost]
    [PermissionAuthorize("advances.create")]
    public async Task<ActionResult<AdvanceSummary>> Create(CreateAdvanceRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId <= 0 || request.Amount <= 0) return BadRequest(new { message = "المشروع وقيمة الدفعة مطلوبان بشكل صحيح." });
        var result = await advancesService.CreateAsync(request, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return result is null ? BadRequest(new { message = "المشروع غير موجود أو لا تملك صلاحية الوصول إليه." }) : Ok(result);
    }

    [HttpGet("{advanceId:int}/allocations")]
    [PermissionAuthorize("advances.view")]
    public async Task<ActionResult<IReadOnlyList<AllocationSummary>>> GetAllocations(int advanceId, CancellationToken cancellationToken) =>
        Ok(await advancesService.GetAllocationsAsync(advanceId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken));

    [HttpPost("{advanceId:int}/allocations")]
    [PermissionAuthorize("advances.allocate")]
    public async Task<ActionResult<AllocationSummary>> Allocate(int advanceId, AllocateAdvanceRequest request, CancellationToken cancellationToken)
    {
        var result = await advancesService.AllocateAsync(advanceId, request, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return result is null ? BadRequest(new { message = "بيانات التخصيص غير صحيحة أو لا ترتبط بنفس المشروع." }) : Ok(result);
    }

    private bool HasGlobalProjectAccess() =>
        HttpContext?.User?.IsInRole("admin") == true ||
        HttpContext?.User?.IsInRole("director") == true ||
        HttpContext?.User?.IsInRole("accountant") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
