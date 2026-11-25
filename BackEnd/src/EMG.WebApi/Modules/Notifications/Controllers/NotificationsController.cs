using System.Security.Claims;
using EMG.Api.Modules.Notifications.Dtos;
using EMG.Api.Modules.Notifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Notifications.Controllers;

[ApiController]
[Authorize]
[PermissionAuthorize("notifications.view")]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationSummary>>> Get([FromQuery] bool unreadOnly = false, CancellationToken cancellationToken = default) =>
        Ok(await notificationService.GetAsync(CurrentUserId(), unreadOnly, cancellationToken));

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken) =>
        await notificationService.MarkReadAsync(CurrentUserId(), id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken) =>
        Ok(new { count = await notificationService.MarkAllReadAsync(CurrentUserId(), cancellationToken) });

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
