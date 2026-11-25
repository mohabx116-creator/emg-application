using System.Security.Claims;
using EMG.Api.Modules.Imports.Services;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.UniversalImports.Controllers;

[ApiController]
[Authorize]
[PermissionAuthorize("project-imports.import-approved-history")]
[Route("api/universal-import/review")]
public sealed class ReviewedWorkbookImportsController(ReviewedWorkbookImportService service) : ControllerBase
{
    [HttpPost("preview")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public Task<ActionResult<ReviewedWorkbookPreview>> Preview(IFormFile file, [FromForm] int projectId,
        CancellationToken cancellationToken) => Execute((actor, admin) => service.PreviewAsync(projectId, file, actor, admin, cancellationToken));

    [HttpPost("batches")]
    public Task<ActionResult<ReviewedWorkbookBatch>> Stage(ReviewedWorkbookStageRequest request, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.StageAsync(request, actor, admin, cancellationToken));

    [HttpGet("projects/{projectId:int}/batches")]
    public Task<ActionResult<IReadOnlyList<int>>> List(int projectId, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.ListAsync(projectId, actor, admin, cancellationToken));

    [HttpGet("batches/{batchId:int}")]
    public Task<ActionResult<ReviewedWorkbookBatch>> Get(int batchId, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.GetAsync(batchId, actor, admin, cancellationToken));

    [HttpGet("projects/{projectId:int}/settlements")]
    public Task<ActionResult<IReadOnlyList<HistoricalSettlementReview>>> Settlements(int projectId, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.ListSettlementReviewsAsync(projectId, actor, admin, cancellationToken));

    [HttpPut("batches/{batchId:int}/settlements")]
    public Task<ActionResult<HistoricalSettlementReview>> ReviewSettlement(int batchId, HistoricalSettlementDecision decision, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.ReviewSettlementAsync(batchId, decision, actor, admin, cancellationToken));

    [HttpPost("batches/{batchId:int}/settlements/{recordId:int}/recognize")]
    public Task<ActionResult<HistoricalSettlementReview>> RecognizeSettlement(int batchId, int recordId, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.RecognizeSettlementAsync(batchId, recordId, actor, admin, cancellationToken));

    [HttpPost("batches/{batchId:int}/approve")]
    public Task<ActionResult<ReviewedWorkbookBatch>> Approve(int batchId, [FromQuery] int? version, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.ApproveAsync(batchId, version, actor, admin, cancellationToken));

    [HttpPut("batches/{batchId:int}/mapping")]
    public Task<ActionResult<ReviewedWorkbookBatch>> Revise(int batchId, ReviewedWorkbookDecision decision, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.ReviseAsync(batchId, decision, actor, admin, cancellationToken));

    [HttpPost("batches/{batchId:int}/post")]
    public Task<ActionResult<ReviewedWorkbookBatch>> Post(int batchId, CancellationToken cancellationToken) =>
        Execute((actor, admin) => service.PostAsync(batchId, actor, admin, cancellationToken));

    private async Task<ActionResult<T>> Execute<T>(Func<int, bool, Task<T>> action)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Forbid();
        try { return Ok(await action(actor, User.IsInRole("admin"))); }
        catch (KeyNotFoundException error) { return NotFound(new { message = error.Message }); }
        catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
        catch (ApprovedHistoryForbiddenException) { return Forbid(); }
        catch (HistoricalWorkbookPostingDisabledException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "HISTORICAL_MUTATIONS_DISABLED", message = "الترحيل المالي التاريخي مغلق." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "تغيرت الدفعة أو يوجد مصدر مكرر. أعد فتح قائمة الدفعات." });
        }
        catch (Exception error) when (error is ArgumentException or System.IO.InvalidDataException or System.Xml.XmlException or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            return BadRequest(new { message = "تعذر قراءة ملف Excel. تحقق من صيغة XLSX وسلامة الملف." });
        }
    }
}
