using EMG.Api.Modules.Attendance.Dtos;

namespace EMG.Api.Modules.Attendance.Services;

public interface IAttendanceService
{
    Task<AttendanceSummary?> GetTodayAsync(int userId, CancellationToken cancellationToken);
    Task<AttendanceSummary?> CheckInAsync(int userId, string? notes, CancellationToken cancellationToken);
    Task<AttendanceSummary?> CheckOutAsync(int userId, string? notes, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendanceSummary>> GetAsync(DateOnly? date, int? userId, bool isAdmin, int currentUserId, CancellationToken cancellationToken);
}
