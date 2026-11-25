using EMG.Api.Modules.Imports.Dtos;
using EMG.Api.Modules.Imports.Services;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EMG.Api.Modules.Imports.Controllers;

[ApiController]
[Authorize]
[Route("api/imports")]
public sealed class ImportsController(IWorkbookImportService importService) : ControllerBase
{
    [HttpPost("workbook-preview")]
    [PermissionAuthorize("data.migrate")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<WorkbookPreview>> WorkbookPreview(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "ملف Excel مطلوب." });
        var preview = await importService.PreviewAsync(file, cancellationToken);
        return preview.IsValid ? Ok(preview) : BadRequest(preview);
    }

    [HttpPost("new-project-plan")]
    [PermissionAuthorize("data.migrate")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<WorkbookMigrationPlan>> NewProjectPlan(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "ملف Excel مطلوب." });
        var plan = await importService.BuildNewProjectPlanAsync(file, cancellationToken);
        return plan is null ? BadRequest(new { message = "تعذر إنشاء خطة ترحيل من الملف." }) : Ok(plan);
    }

    [HttpPost("existing-project-plan")]
    [PermissionAuthorize("data.migrate")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<WorkbookMigrationPlan>> ExistingProjectPlan(IFormFile file, [FromForm] int projectId, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "ملف Excel مطلوب." });
        var plan = await importService.BuildExistingProjectPlanAsync(file, projectId, cancellationToken);
        return plan is null ? NotFound(new { message = "المشروع المطلوب غير موجود أو تعذر إنشاء خطة التحديث." }) : Ok(plan);
    }

    [HttpPost("commit-new-project")]
    [PermissionAuthorize("data.migrate")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ImportedProjectResult>> CommitNewProject(
        IFormFile file,
        [FromForm] string? projectName,
        [FromForm] string? projectCode,
        [FromForm] string? clientName,
        [FromForm] string? location,
        [FromForm] string? engineerName,
        CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "ملف Excel مطلوب." });
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : (int?)null;
        try
        {
            var result = await importService.CommitNewProjectAsync(file, projectName, projectCode, clientName, location, engineerName, userId, cancellationToken);
            return result is null
                ? Conflict(new { message = "تعذر اعتماد الترحيل. راجع خطة المطابقة والصفحات التي تحتاج مراجعة." })
                : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "تعذر حفظ المشروع أو بياناته. تحقق من كود المشروع وعدم وجود بيانات مكررة ثم أعد المحاولة." });
        }
    }

    [HttpPost("existing-project-update")]
    [PermissionAuthorize("data.migrate")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ImportedProjectResult>> CommitExistingProject(IFormFile file, [FromForm] int projectId, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "ملف Excel مطلوب." });
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : (int?)null;
        try
        {
            var result = await importService.CommitExistingProjectAsync(file, projectId, userId, cancellationToken);
            return result is null
                ? Conflict(new { message = "تعذر تحديث المشروع. راجع خطة المطابقة وتأكد من عدم وجود بيانات تشغيلية غير مقروءة." })
                : Ok(result);
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "تعذر حفظ تحديث المشروع. لم يتم اعتماد أي جزء من العملية." }); }
    }

    [HttpPost("approved-history")]
    [PermissionAuthorize("project-imports.import-approved-history")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ImportedProjectResult>> CommitApprovedHistory(
        IFormFile file, [FromForm] int? projectId, [FromForm] bool confirmedApproved, [FromForm] DateOnly? closedThrough,
        CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "ملف Excel مطلوب." });
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        try
        {
            return Ok(await importService.CommitApprovedHistoryAsync(file, projectId, confirmedApproved, closedThrough,
                userId, User.IsInRole("admin"), cancellationToken));
        }
        catch (ApprovedHistoryForbiddenException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "لا تملك صلاحية استيراد الأرشيف المعتمد لهذا المشروع أو لهذه القيمة." });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "تعذر حفظ الأرشيف المعتمد. لم يتم اعتماد أي جزء منه." });
        }
    }
}
