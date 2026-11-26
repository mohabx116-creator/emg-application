using System.Security.Claims;
using EMG.Api.Modules.Security.Authorization;
using EMG.Api.Modules.Transactions.Dtos;
using EMG.Api.Modules.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.Transactions.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(ITransactionCorrectionService service) : ControllerBase
{
    [HttpPut("{entityType}/{entityId:int}")]
    [PermissionAuthorize("transactions.correct")]
    public async Task<ActionResult<TransactionOperationResult>> Correct(string entityType, int entityId, TransactionCorrectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CorrectAsync(entityType, entityId, request, CurrentUserId(), GlobalAccess(), cancellationToken);
            return result is null ? BadRequest(new { message = "التصحيح غير مسموح أو السجل غير موجود." }) : Ok(result);
        }
        catch (TransactionValidationException exception) { return UnprocessableEntity(new { message = exception.Message, checks = exception.Errors }); }
    }

    [HttpPost("{entityType}/{entityId:int}/cancel")]
    [PermissionAuthorize("transactions.cancel")]
    public async Task<ActionResult<TransactionOperationResult>> Cancel(string entityType, int entityId, TransactionCancellationRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CancelAsync(entityType, entityId, request, CurrentUserId(), GlobalAccess(), cancellationToken);
        return result is null ? BadRequest(new { message = "الإلغاء غير مسموح أو توجد آثار مالية تابعة." }) : Ok(result);
    }

    private bool GlobalAccess() => User.IsInRole("admin") || User.IsInRole("director");
    private int? CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
