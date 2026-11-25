using System.Security.Claims;
using EMG.Api.Modules.Attachments.Dtos;
using EMG.Api.Modules.Attachments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Attachments.Controllers;

[ApiController]
[Authorize]
[Route("api/attachments")]
public sealed class AttachmentsController(IAttachmentsService attachmentsService) : ControllerBase
{
    [HttpGet("{entityType}/{entityId:int}")]
    [PermissionAuthorize("attachments.view")]
    public async Task<ActionResult<IReadOnlyList<AttachmentSummary>>> Get(string entityType, int entityId, CancellationToken cancellationToken) =>
        Ok(await attachmentsService.GetAsync(entityType, entityId, CurrentUserId(), IsCanonicalAdmin(), cancellationToken));

    [HttpPost("{entityType}/{entityId:int}")]
    [PermissionAuthorize("attachments.upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<AttachmentSummary>> Upload(string entityType, int entityId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "الملف مطلوب." });
        var attachment = await attachmentsService.UploadAsync(entityType, entityId, file, CurrentUserId(), IsCanonicalAdmin(), cancellationToken);
        return attachment is null ? BadRequest(new { message = "نوع الملف أو الكيان غير مدعوم، أو حجم الملف أكبر من المسموح." }) : Ok(attachment);
    }

    [HttpGet("{id:int}/download")]
    [PermissionAuthorize("attachments.view")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var download = await attachmentsService.GetDownloadAsync(id, CurrentUserId(), IsCanonicalAdmin(), cancellationToken);
        return download is null ? NotFound() : PhysicalFile(download.FilePath, download.ContentType, download.FileName);
    }

    private bool IsCanonicalAdmin() => HttpContext?.User?.IsInRole("admin") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
