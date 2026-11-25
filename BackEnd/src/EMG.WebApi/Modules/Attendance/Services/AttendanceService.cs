using EMG.Api.Modules.Attendance.Dtos;
using EMG.Domain.Security;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Attendance.Services;

public sealed class AttendanceService(EmgDbContext db) : IAttendanceService
{
    public async Task<AttendanceSummary?> GetTodayAsync(int userId, CancellationToken cancellationToken) =>
        await db.AttendanceRecords.AsNoTracking().Where(item => item.UserId == userId && item.AttendanceDate == Today())
            .Select(ToSummary()).FirstOrDefaultAsync(cancellationToken);

    public async Task<AttendanceSummary?> CheckInAsync(int userId, string? notes, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(item => item.Id == userId && item.IsActive, cancellationToken)) return null;
        var today = Today();
        var existing = await db.AttendanceRecords.Include(item => item.User).FirstOrDefaultAsync(item => item.UserId == userId && item.AttendanceDate == today, cancellationToken);
        if (existing is not null) return ToSummary(existing);
        var record = new AttendanceRecord { UserId = userId, AttendanceDate = today, CheckInAtUtc = DateTime.UtcNow, Notes = notes?.Trim() };
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(record).Reference(item => item.User).LoadAsync(cancellationToken);
        return ToSummary(record);
    }

    public async Task<AttendanceSummary?> CheckOutAsync(int userId, string? notes, CancellationToken cancellationToken)
    {
        var record = await db.AttendanceRecords.Include(item => item.User).FirstOrDefaultAsync(item => item.UserId == userId && item.AttendanceDate == Today(), cancellationToken);
        if (record is null) return null;
        record.CheckOutAtUtc ??= DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes)) record.Notes = notes.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(record);
    }

    public async Task<IReadOnlyList<AttendanceSummary>> GetAsync(DateOnly? date, int? userId, bool isAdmin, int currentUserId, CancellationToken cancellationToken)
    {
        var query = db.AttendanceRecords.AsNoTracking();
        if (!isAdmin) query = query.Where(item => item.UserId == currentUserId);
        else if (userId is not null) query = query.Where(item => item.UserId == userId);
        if (date is not null) query = query.Where(item => item.AttendanceDate == date);
        return await query.OrderByDescending(item => item.AttendanceDate).ThenBy(item => item.UserId).Select(ToSummary()).ToListAsync(cancellationToken);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
    private static System.Linq.Expressions.Expression<Func<AttendanceRecord, AttendanceSummary>> ToSummary() => item => new AttendanceSummary(
        item.Id, item.UserId, item.User.DisplayName, item.AttendanceDate, item.CheckInAtUtc, item.CheckOutAtUtc, item.Status, item.Notes ?? string.Empty);
    private static AttendanceSummary ToSummary(AttendanceRecord item) => new(item.Id, item.UserId, item.User?.DisplayName ?? string.Empty, item.AttendanceDate, item.CheckInAtUtc, item.CheckOutAtUtc, item.Status, item.Notes ?? string.Empty);
}
