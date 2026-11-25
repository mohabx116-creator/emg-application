namespace EMG.Api.Modules.Attendance.Dtos;

public sealed record AttendanceSummary(
    int Id,
    int UserId,
    string UserName,
    DateOnly AttendanceDate,
    DateTime CheckInAtUtc,
    DateTime? CheckOutAtUtc,
    string Status,
    string Notes);
