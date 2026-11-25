using System.Security.Claims;
using EMG.Api.Modules.Intelligence.Dtos;
using EMG.Api.Modules.Intelligence.Services;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.Intelligence.Controllers;

[ApiController]
[Authorize]
[Route("api/intelligence/entry")]
public sealed class EntryIntelligenceController(IEntryIntelligenceService intelligenceService) : ControllerBase
{
    [HttpPost("suggest")]
    [PermissionAuthorize("site-movements.create")]
    public async Task<ActionResult<EntrySuggestionResponse>> Suggest(EntrySuggestionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description)) return BadRequest(new { message = "وصف الحركة مطلوب لإعطاء اقتراح آمن." });
        return Ok(await intelligenceService.SuggestAsync(request, cancellationToken));
    }
}
