using System.Security.Claims;
using EMG.Api.Modules.Attendance.Dtos;
using EMG.Api.Modules.Attendance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Security.Authorization;

namespace EMG.Api.Modules.Attendance.Controllers;

[ApiController]
[Authorize]
[Authorize]
[Route("api/attendance")]
public sealed class AttendanceController(IAttendanceService attendanceService) : ControllerBase
{
    [HttpGet("today")]
    [PermissionAuthorize("attendance.view")]
    public async Task<ActionResult<AttendanceSummary>> Today(CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetTodayAsync(CurrentUserId(), cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpPost("check-in")]
    [PermissionAuthorize("attendance.check-in")]
    public async Task<ActionResult<AttendanceSummary>> CheckIn(AttendanceRequest request, CancellationToken cancellationToken)
    {
        var result = await attendanceService.CheckInAsync(CurrentUserId(), request.Notes, cancellationToken);
        return result is null ? BadRequest() : Ok(result);
    }

    [HttpPost("check-out")]
    [PermissionAuthorize("attendance.check-out")]
    public async Task<ActionResult<AttendanceSummary>> CheckOut(AttendanceRequest request, CancellationToken cancellationToken)
    {
        var result = await attendanceService.CheckOutAsync(CurrentUserId(), request.Notes, cancellationToken);
        return result is null ? BadRequest(new { message = "يجب تسجيل الحضور أولًا." }) : Ok(result);
    }

    [HttpGet]
    [PermissionAuthorize("attendance.view")]
    public async Task<ActionResult<IReadOnlyList<AttendanceSummary>>> Get(DateOnly? date, int? userId, CancellationToken cancellationToken) =>
        Ok(await attendanceService.GetAsync(date, userId, IsAdmin(), CurrentUserId(), cancellationToken));

private bool IsAdmin() => HttpContext?.User?.IsInRole("admin") == true || HttpContext?.User?.IsInRole("director") == true;
    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
