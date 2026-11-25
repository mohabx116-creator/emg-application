using System.Security.Claims;
using System.Xml;
using EMG.Api.Modules.ReportBuilder.Dtos;
using EMG.Api.Modules.ReportBuilder.Services;
using EMG.Api.Modules.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMG.Api.Modules.ReportBuilder.Controllers;

[ApiController]
[Authorize]
[Route("api/reports/builder")]
public sealed class ReportBuilderController(IReportBuilderService service, ILogger<ReportBuilderController> logger) : ControllerBase
{
    [HttpPost("preview")]
    [PermissionAuthorize("reports.view")]
    public async Task<ActionResult<ReportBuilderPreview>> Preview([FromBody] ReportBuilderQuery query, CancellationToken cancellationToken)
    {
        return Ok(await service.PreviewAsync(query, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken));
    }

    [HttpPost("export.xlsx")]
    [PermissionAuthorize("reports.export")]
    public async Task<IActionResult> ExportExcel([FromBody] ReportBuilderWorkbookRequest request, CancellationToken cancellationToken)
    {
        if (!ReportBuilderXlsxWriter.Supports(request.Presentation))
            return BadRequest(new { code = "XLSX_EXPORT_INVALID_PRESENTATION", message = "عرض Excel المحدد غير مدعوم." });

        try
        {
            var preview = await service.PreviewAsync(request.Query, CurrentUserId(), HasGlobalProjectAccess(), cancellationToken,
                includeProjectAmounts: request.Presentation != "custom");
            var workbook = ReportBuilderXlsxWriter.Build(preview, request.Presentation, cancellationToken);
            return File(workbook, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "emg-financial-report.xlsx");
        }
        catch (ReportWorkbookException exception)
        {
            return UnprocessableEntity(new { code = exception.Code, message = exception.Message, section = exception.Section,
                actual = exception.Actual, capacity = exception.Capacity });
        }
        catch (XmlException)
        {
            return UnprocessableEntity(new { code = "XLSX_EXPORT_UNSUPPORTED_VALUE", message = "إحدى قيم التقرير تحتوي على أحرف غير صالحة في Excel." });
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            logger.LogError(exception, "Report Builder could not read or write the XLSX package.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { code = "XLSX_EXPORT_UNAVAILABLE", message = "تعذّر إنشاء ملف Excel. تواصل مع مسؤول النظام." });
        }
    }

    private bool HasGlobalProjectAccess() => User.IsInRole("admin") || User.IsInRole("director") || User.IsInRole("accountant");
    private int? CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
