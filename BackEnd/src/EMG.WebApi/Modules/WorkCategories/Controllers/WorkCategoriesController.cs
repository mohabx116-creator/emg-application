using System.Security.Claims;
using EMG.Api.Modules.Security.Authorization;
using EMG.Api.Modules.WorkCategories.Dtos;
using EMG.Infrastructure.Db;
using EMG.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.WorkCategories.Controllers;

[ApiController]
[Authorize]
[Route("api/work-categories")]
public sealed class WorkCategoriesController(EmgDbContext db) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("work-items.view")]
    public async Task<ActionResult<IReadOnlyList<WorkCategorySummary>>> Get(int? projectId, string? classification, CancellationToken ct)
    {
        var query = db.ProjectWorkCategories.AsNoTracking().Where(item => item.IsActive && (item.ProjectId == null || item.ProjectId == projectId));
        if (!string.IsNullOrWhiteSpace(classification)) query = query.Where(item => item.Classification == classification.Trim());
        return Ok(await query.OrderBy(item => item.Name).Select(item => new WorkCategorySummary(item.Id, item.ProjectId, item.Name, item.Classification, item.IsActive)).ToListAsync(ct));
    }

    [HttpPost]
    [PermissionAuthorize("work-items.manage")]
    public async Task<ActionResult<WorkCategorySummary>> Create(CreateWorkCategoryRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150) return BadRequest(new { message = "اسم التصنيف مطلوب." });
        var classification = string.IsNullOrWhiteSpace(request.Classification) ? "التشطيبات" : request.Classification.Trim();
        if (await db.ProjectWorkCategories.AnyAsync(item => item.ProjectId == request.ProjectId && item.Name.ToUpper() == name.ToUpper(), ct))
            return Conflict(new { message = "التصنيف موجود بالفعل." });
        var actor = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        if (actor <= 0) return Unauthorized();
        var category = new ProjectWorkCategory { ProjectId = request.ProjectId, Name = name, Classification = classification, CreatedByUserId = actor };
        db.ProjectWorkCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return Created($"/api/work-categories/{category.Id}", new WorkCategorySummary(category.Id, category.ProjectId, category.Name, category.Classification, category.IsActive));
    }
}
