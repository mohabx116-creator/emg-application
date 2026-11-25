using EMG.Api.Modules.Projects.Dtos;
using EMG.Api.Modules.Projects.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Projects.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class ProjectsController(IProjectsService projectsService) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("projects.view")]
    public async Task<ActionResult<IReadOnlyList<ProjectSummary>>> GetProjects(CancellationToken cancellationToken)
    {
        return Ok(await projectsService.GetProjectsAsync(CurrentUserId(), HasGlobalProjectAccess(), cancellationToken));
    }

    [HttpGet("{id:int}")]
    [PermissionAuthorize("projects.view")]
    public async Task<ActionResult<ProjectSummary>> GetProject(int id, CancellationToken cancellationToken)
    {
        var project = await projectsService.GetProjectAsync(id, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    private bool HasGlobalProjectAccess() =>
        HttpContext?.User?.IsInRole("admin") == true ||
        HttpContext?.User?.IsInRole("director") == true ||
        HttpContext?.User?.IsInRole("accountant") == true;

    private int? CurrentUserId()
    {
        var principal = HttpContext?.User;
        return principal is not null && int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    [HttpPost]
    [PermissionAuthorize("projects.create")]
    public async Task<ActionResult<ProjectSummary>> CreateProject([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "اسم المشروع وكود المشروع مطلوبان." });
        if (request.OfficeSharePercentage is < 0 or > 100)
            return BadRequest(new { message = "نسبة المكتب يجب أن تكون بين 0 و100." });

        var project = await projectsService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
    }

    [HttpPut("{id:int}")]
    [PermissionAuthorize("projects.update")]
    public async Task<ActionResult<ProjectSummary>> UpdateProject(int id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "اسم المشروع وكود المشروع مطلوبان." });
        if (request.OfficeSharePercentage is < 0 or > 100)
            return BadRequest(new { message = "نسبة المكتب يجب أن تكون بين 0 و100." });

        var project = await projectsService.UpdateAsync(id, request, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost("{id:int}/import-approval")]
    [PermissionAuthorize("project-imports.approve")]
    public async Task<ActionResult<ProjectSummary>> ApproveImport(int id, [FromBody] ProjectImportDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await projectsService.DecideImportAsync(id, true, request.Notes, CurrentUserId(), cancellationToken);
            return project is null ? BadRequest(new { message = "المشروع غير موجود أو لا يملك عملية ترحيل معلقة." }) : Ok(project);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{id:int}/import-rejection")]
    [PermissionAuthorize("project-imports.reject")]
    public async Task<ActionResult<ProjectSummary>> RejectImport(int id, [FromBody] ProjectImportDecisionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Notes)) return BadRequest(new { message = "سبب رفض الترحيل مطلوب." });
        var project = await projectsService.DecideImportAsync(id, false, request.Notes, CurrentUserId(), cancellationToken);
        return project is null ? BadRequest(new { message = "المشروع غير موجود أو لا يملك عملية ترحيل معلقة." }) : Ok(project);
    }
}
