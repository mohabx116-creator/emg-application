using System.Security.Claims;
using System.Text;
using EMG.Api.Modules.Reports.Dtos;
using EMG.Api.Modules.Reports.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Reports.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(IReportsService reportsService) : ControllerBase
{
    [HttpGet("projects/{projectId:int}")]
    [PermissionAuthorize("reports.view")]
    public async Task<ActionResult<ProjectReportSummary>> ProjectReport(int projectId, CancellationToken cancellationToken)
    {
        var report = await reportsService.GetProjectReportAsync(projectId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("projects/{projectId:int}/movements.csv")]
    [PermissionAuthorize("reports.export")]
    public async Task<IActionResult> ProjectMovementsCsv(int projectId, CancellationToken cancellationToken)
    {
        var csv = await reportsService.ExportProjectMovementsCsvAsync(projectId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return csv is null ? NotFound() : File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"project-{projectId}-movements.csv");
    }

    [HttpGet("projects/{projectId:int}/audit")]
    [PermissionAuthorize("reports.view")]
    public async Task<ActionResult<OperationalAuditSummary>> OperationalAudit(int projectId, CancellationToken cancellationToken)
    {
        var audit = await reportsService.GetOperationalAuditAsync(projectId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return audit is null ? NotFound() : Ok(audit);
    }

    [HttpGet("projects/{projectId:int}/historical")]
    [PermissionAuthorize("reports.view")]
    public async Task<ActionResult<HistoricalProjectReport>> HistoricalProject(int projectId, CancellationToken cancellationToken)
    {
        var report = await reportsService.GetHistoricalProjectReportAsync(projectId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("projects/{projectId:int}.xlsx")]
    [PermissionAuthorize("reports.export")]
    public async Task<IActionResult> ProjectWorkbook(int projectId, CancellationToken cancellationToken)
    {
        try
        {
            var workbook = await reportsService.ExportProjectWorkbookAsync(projectId, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken);
            return workbook is null
                ? NotFound()
                : File(workbook, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"project-{projectId}-report.xlsx");
        }
        catch (XlsxExportCapacityException exception)
        {
            return UnprocessableEntity(new { code = "XLSX_EXPORT_CAPACITY_EXCEEDED", message = exception.Message, sheet = exception.Sheet, actual = exception.Actual, capacity = exception.Capacity });
        }
    }

    private bool HasGlobalProjectAccess() =>
        HttpContext?.User?.IsInRole("admin") == true ||
        HttpContext?.User?.IsInRole("director") == true ||
        HttpContext?.User?.IsInRole("accountant") == true;
    private int? CurrentUserId() => HttpContext?.User is not null && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
