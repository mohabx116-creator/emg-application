using System.Security.Claims;
using EMG.Api.Modules.Search.Dtos;
using EMG.Api.Modules.Search.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Search.Controllers;

[ApiController]
[Authorize]
[PermissionAuthorize("search.view")]
[Route("api/search")]
public sealed class SearchController(ISearchService searchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchResult>>> Search([FromQuery] string q, CancellationToken cancellationToken) =>
        Ok(await searchService.SearchAsync(q ?? string.Empty, CurrentUserId(), IsAdmin(), cancellationToken));

private bool IsAdmin() => HttpContext?.User?.IsInRole("admin") == true || HttpContext?.User?.IsInRole("director") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
