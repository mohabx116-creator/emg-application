using System.Security.Claims;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.UniversalImports.Controllers;

[ApiController]
[Authorize]
[Route("api/universal-import/batches")]
public sealed class HistoricalImportPostingController(IHistoricalImportPostingService postingService) : ControllerBase
{
    [HttpPost]
    [PermissionAuthorize("data.migrate")]
    public async Task<ActionResult<HistoricalImportBatchReport>> Stage(HistoricalImportStageRequest request, CancellationToken cancellationToken)
    {
        if (!TryActor(out var actor)) return Forbid();
        try { return Ok(await postingService.StageAsync(request, actor, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("{batchId:int}")]
    [PermissionAuthorize("data.migrate")]
    public async Task<ActionResult<HistoricalImportBatchReport>> Get(int batchId, CancellationToken cancellationToken)
    {
        try { return Ok(await postingService.GetAsync(batchId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("{batchId:int}/approve")]
    [PermissionAuthorize("project-imports.import-approved-history")]
    public Task<ActionResult<HistoricalImportBatchReport>> Approve(int batchId, CancellationToken cancellationToken) => Execute(() => postingService.ApproveAsync(batchId, Actor(), cancellationToken));

    [HttpPost("{batchId:int}/post")]
    [PermissionAuthorize("project-imports.import-approved-history")]
    public Task<ActionResult<HistoricalImportBatchReport>> Post(int batchId, CancellationToken cancellationToken) => Execute(() => postingService.PostAsync(batchId, Actor(), cancellationToken));

    [HttpPost("{batchId:int}/reject")]
    [PermissionAuthorize("project-imports.import-approved-history")]
    public Task<ActionResult<HistoricalImportBatchReport>> Reject(int batchId, [FromBody] string reason, CancellationToken cancellationToken) => Execute(() => postingService.RejectAsync(batchId, Actor(), reason, cancellationToken));

    private async Task<ActionResult<HistoricalImportBatchReport>> Execute(Func<Task<HistoricalImportBatchReport>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (DbUpdateException) { return Conflict(new { code = "HISTORICAL_POSTING_CONFLICT", message = "The historical batch was already posted or changed by a concurrent request." }); }
    }

    private bool TryActor(out int actor) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private int Actor() => TryActor(out var actor) ? actor : throw new UnauthorizedAccessException();
}
