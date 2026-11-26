using System.Security.Claims;
using EMG.Api.Modules.SiteMovements.Dtos;
using EMG.Api.Modules.SiteMovements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.SiteMovements.Controllers;

[ApiController]
[Authorize]
[Route("api/site-movements")]
public sealed class SiteMovementsController(ISiteMovementsService siteMovementsService, EMG.Infrastructure.Db.EmgDbContext db) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("site-movements.view")]
    public async Task<ActionResult<IReadOnlyList<SiteMovementSummary>>> Get(int? projectId, string? movementType, CancellationToken cancellationToken) =>
        Ok(await siteMovementsService.GetAsync(projectId, movementType, CurrentUserId(), IsAdmin(), cancellationToken));

    [HttpGet("{id:int}")]
    [PermissionAuthorize("site-movements.view")]
    public async Task<ActionResult<SiteMovementSummary>> GetById(int id, CancellationToken cancellationToken)
    {
        var movement = await siteMovementsService.GetByIdAsync(id, CurrentUserId(), IsAdmin(), cancellationToken);
        return movement is null ? NotFound() : Ok(movement);
    }

    [HttpPost]
    [PermissionAuthorize("site-movements.create")]
    public async Task<ActionResult<SiteMovementSummary>> Create(CreateSiteMovementRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId <= 0 || string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.MovementType) || request.Amount < 0 || request.Quantity < 0 || request.UnitPrice < 0)
            return BadRequest(new { message = "المشروع ونوع الحركة والوصف والبيانات المالية مطلوبة بشكل صحيح." });
        if (request.Amount is null && (request.Quantity is null || request.UnitPrice is null))
            return BadRequest(new { message = "أدخل قيمة الحركة مباشرة أو الكمية وسعر الوحدة." });
        if (request.MovementType.Trim() == "مصروفات" && string.IsNullOrWhiteSpace(request.ExpenseCategory))
            return BadRequest(new { message = "بند المصروف مطلوب للمصروفات اليومية." });
        var movement = await siteMovementsService.CreateAsync(request, CurrentUserId(), IsAdmin(), cancellationToken);
        return movement is null ? NotFound(new { message = "المشروع أو المورد غير موجود، أو لا تملك صلاحية المشروع." }) : Ok(movement);
    }

    [HttpPost("batch")]
    [PermissionAuthorize("site-movements.create")]
    public async Task<ActionResult<IReadOnlyList<SiteMovementSummary>>> CreateBatch(CreateSiteMovementBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Movements is null || request.Movements.Count == 0 || request.Movements.Any(item => item.ProjectId <= 0 || string.IsNullOrWhiteSpace(item.Description) || string.IsNullOrWhiteSpace(item.MovementType) || item.Amount < 0 || item.Quantity < 0 || item.UnitPrice < 0))
            return BadRequest(new { message = "بيانات الحركات غير صحيحة." });
        var movements = await siteMovementsService.CreateBatchAsync(request, CurrentUserId(), IsAdmin(), cancellationToken);
        return movements is null ? BadRequest(new { message = "المشروع أو المورد غير موجود، أو لا تملك صلاحية المشروع." }) : Ok(movements);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SiteMovementSummary>> Update(int id, CreateSiteMovementRequest request, CancellationToken cancellationToken)
    {
        if (request.ProjectId <= 0 || string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.MovementType) || request.Amount < 0 || request.Quantity < 0 || request.UnitPrice < 0)
            return BadRequest(new { message = "بيانات الحركة غير صحيحة." });
        if (request.Amount is null && (request.Quantity is null || request.UnitPrice is null)) return BadRequest(new { message = "أدخل قيمة الحركة مباشرة أو الكمية وسعر الوحدة." });
        if (request.MovementType.Trim() == "مصروفات" && string.IsNullOrWhiteSpace(request.ExpenseCategory)) return BadRequest(new { message = "بند المصروف مطلوب للمصروفات اليومية." });
        var movement = await siteMovementsService.UpdateAsync(id, request, CurrentUserId(), IsAdmin(), cancellationToken, await HasPermissionAsync("transactions.correct"));
        return movement is null ? BadRequest(new { message = "لا يمكن تعديل الحركة الحالية أو لا تملك الصلاحية." }) : Ok(movement);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await siteMovementsService.DeleteAsync(id, CurrentUserId(), IsAdmin(), cancellationToken, await HasPermissionAsync("transactions.cancel"))
            ? NoContent()
            : BadRequest(new { message = "لا يمكن حذف الحركة: لا تملك الصلاحية أو توجد اعتماديات مالية تابعة." });

    private bool IsAdmin() => HttpContext?.User?.IsInRole("admin") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private async Task<bool> HasPermissionAsync(string code) => CurrentUserId() is int userId && await db.UserRoles.Where(link => link.UserId == userId && link.User.IsActive).AnyAsync(link => link.Role.Name == "admin" || link.Role.RolePermissions.Any(permission => permission.Permission.Code == code));
}
