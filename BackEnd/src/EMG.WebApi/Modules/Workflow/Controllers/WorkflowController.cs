using System.Security.Claims;
using EMG.Api.Modules.Workflow.Dtos;
using EMG.Api.Modules.Workflow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Workflow.Controllers;

[ApiController]
[Authorize]
[Route("api/workflow")]
public sealed class WorkflowController(IWorkflowService workflowService) : ControllerBase
{
    [HttpPost("{entityType}/{entityId:int}/{workflowAction}")]
    public async Task<ActionResult<WorkflowResult>> Execute(string entityType, int entityId, string workflowAction, WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        WorkflowResult? result;
        try
        {
            result = await workflowService.ExecuteAsync(entityType, entityId, workflowAction, request.Notes, CurrentUserId(), IsCanonicalAdmin(), cancellationToken);
        }
        catch (WorkflowValidationException exception)
        {
            return UnprocessableEntity(new { message = "لا يمكن تنفيذ الإجراء قبل استكمال مراجعة الحركة.", checks = exception.Checks });
        }
        catch (WorkflowForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (WorkflowConcurrencyException exception)
        {
            return Conflict(new
            {
                code = "WORKFLOW_CONCURRENCY_CONFLICT",
                message = exception.Message,
                entityType = exception.EntityType,
                entityId = exception.EntityId
            });
        }
        return result is null ? BadRequest(new { message = "الإجراء غير مسموح للحالة الحالية." }) : Ok(result);
    }

    [HttpGet("{entityType}/{entityId:int}/history")]
    [PermissionAuthorize("transactions.history")]
    public async Task<ActionResult<IReadOnlyList<WorkflowResult>>> History(string entityType, int entityId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !await workflowService.CanViewHistoryAsync(entityType, entityId, userId, IsCanonicalAdmin(), cancellationToken)) return Forbid();
        return Ok(await workflowService.GetHistoryAsync(entityType, entityId, cancellationToken));
    }

    [HttpPost("supplier-supply/{entityId:int}/correction")]
    [PermissionAuthorize("transactions.correct")]
    public async Task<IActionResult> CorrectSupply(int entityId, CorrectSupplyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await workflowService.CorrectSupplyAsync(entityId, request, CurrentUserId(), IsCanonicalAdmin(), cancellationToken)
                ? NoContent()
                : BadRequest(new { message = "يمكن تصحيح التوريدة المرفوضة فقط. حدّث البيانات قبل إعادة المحاولة." });
        }
        catch (WorkflowValidationException exception)
        {
            return UnprocessableEntity(new { message = exception.Checks.FirstOrDefault(), checks = exception.Checks });
        }
        catch (WorkflowForbiddenException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
    }

    [HttpGet("{entityType}/{entityId:int}/audit")]
    public async Task<ActionResult<WorkflowAuditResult>> Audit(string entityType, int entityId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var result = await workflowService.AuditAsync(entityType, entityId, userId, IsCanonicalAdmin(), cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }

    [HttpGet("review-queue")]
    public async Task<ActionResult<IReadOnlyList<WorkflowReviewItem>>> ReviewQueue(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        return Ok(await workflowService.GetReviewQueueAsync(userId, IsCanonicalAdmin(), cancellationToken));
    }

    private bool IsCanonicalAdmin() => HttpContext?.User?.IsInRole("admin") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
