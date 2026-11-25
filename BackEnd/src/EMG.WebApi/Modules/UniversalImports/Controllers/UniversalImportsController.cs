using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.UniversalImports.Controllers;

[ApiController]
[Authorize]
[Route("api/universal-import")]
public sealed class UniversalImportsController(IUniversalImportPreviewService previewService) : ControllerBase
{
    [HttpPost("preview")]
    [PermissionAuthorize("data.migrate")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<UniversalPreviewResponse>> Preview(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new { message = "An XLSX file is required." });
        try { return Ok(await previewService.PreviewAsync(file, cancellationToken)); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
