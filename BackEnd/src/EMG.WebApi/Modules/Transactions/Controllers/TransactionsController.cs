using System.Security.Claims;
using EMG.Api.Modules.Security.Authorization;
using EMG.Api.Modules.Transactions.Dtos;
using EMG.Api.Modules.Transactions.Services;
using EMG.Infrastructure.Db;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.Transactions.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(ITransactionCorrectionService service, EmgDbContext db) : ControllerBase
{
    [HttpPut("{entityType}/{entityId:int}")]
    public async Task<ActionResult<TransactionOperationResult>> Correct(string entityType, int entityId, TransactionCorrectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CorrectAsync(entityType, entityId, request, CurrentUserId(), GlobalAccess(), await HasPermissionAsync("transactions.correct"), cancellationToken);
            return result is null ? BadRequest(new { message = "التصحيح غير مسموح أو السجل غير موجود." }) : Ok(result);
        }
        catch (TransactionValidationException exception) { return UnprocessableEntity(new { message = exception.Message, checks = exception.Errors }); }
    }

    [HttpDelete("{entityType}/{entityId:int}")]
    public async Task<ActionResult<TransactionOperationResult>> Delete(string entityType, int entityId, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(entityType, entityId, CurrentUserId(), GlobalAccess(), await HasPermissionAsync("transactions.cancel"), cancellationToken);
        return result is null ? BadRequest(new { message = "لا يمكن حذف السجل: لا تملك الصلاحية، أو يوجد اعتماد مالي تابع، أو السجل غير موجود." }) : Ok(result);
    }

    // Compatibility endpoint: the operation is now permanent deletion; the old reason is ignored.
    [HttpPost("{entityType}/{entityId:int}/cancel")]
    public Task<ActionResult<TransactionOperationResult>> CancelCompatibility(string entityType, int entityId, TransactionCancellationRequest _, CancellationToken cancellationToken) => Delete(entityType, entityId, cancellationToken);

    private bool GlobalAccess() => User.IsInRole("admin") || User.IsInRole("director");
    private int? CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private async Task<bool> HasPermissionAsync(string code) => CurrentUserId() is int userId && await db.UserRoles
        .Where(link => link.UserId == userId && link.User.IsActive)
        .AnyAsync(link => link.Role.Name == "admin" || link.Role.RolePermissions.Any(permission => permission.Permission.Code == code));
}
