using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EMG.Api.Modules.UniversalImports.Controllers;

[ApiController]
[Authorize]
[Route("api/universal-import")]
public sealed class UniversalImportsController(IUniversalImportPreviewService previewService, ReviewedWorkbookSnapshots snapshots,
    ReviewedWorkbookImportService imports) : ControllerBase
{
    [HttpPost("preview")]
    [PermissionAuthorize("data.migrate")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<UniversalPreviewResponse>> Preview(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "An XLSX file is required." });
        try
        {
            if (!TryActor(out var actor)) return Forbid();
            var preview = await previewService.PreviewAsync(file, cancellationToken);
            if (preview.Records.Count > 20000 || preview.FileName.Length > 260)
                return BadRequest(new { message = "الملف يتجاوز حدود المعاينة." });
            return Ok(preview with { SnapshotId = snapshots.Store(actor, 0, preview) });
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (Exception exception) when (exception is ArgumentException or System.IO.InvalidDataException or System.Xml.XmlException or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            return BadRequest(new { message = "تعذر قراءة ملف Excel. تحقق من صيغة XLSX وسلامة الملف." });
        }
    }

    [HttpPost("revalidate")]
    [PermissionAuthorize("data.migrate")]
    public async Task<ActionResult<UniversalNewProjectValidation>> Revalidate(UniversalNewProjectRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor)) return Forbid();
        try
        {
            var validation = await imports.RevalidateNewProjectAsync(request, actor, ct);
            return Ok(validation with { PostingEnabled = validation.PostingEnabled && User.IsInRole("admin") });
        }
        catch (KeyNotFoundException error) { return NotFound(new { message = error.Message }); }
        catch (InvalidOperationException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPost("commit")]
    [PermissionAuthorize("data.migrate")]
    [PermissionAuthorize("projects.create")]
    public async Task<ActionResult<UniversalNewProjectResult>> Commit(UniversalNewProjectRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actor) || !User.IsInRole("admin")) return Forbid();
        try { return Ok(await imports.CommitNewProjectAsync(request, actor, ct)); }
        catch (KeyNotFoundException error) { return NotFound(new { message = error.Message }); }
        catch (HistoricalWorkbookPostingDisabledException)
        {
            return StatusCode(403, new { message = "ترحيل ملفات الشركة غير مفعّل لهذه البيئة أو لهذا الحساب." });
        }
        catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
        catch (Exception error) when (error is DbUpdateException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            return Conflict(new { message = "تعذر حفظ الاستيراد بالكامل. تحقق من عدم تكرار المصدر أو رقم المشروع." });
        }
    }

    private bool TryActor(out int actor) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
}
