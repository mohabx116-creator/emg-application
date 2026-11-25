namespace EMG.Domain.Security;

public sealed class AttendanceRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateOnly AttendanceDate { get; set; }
    public DateTime CheckInAtUtc { get; set; }
    public DateTime? CheckOutAtUtc { get; set; }
    public string Status { get; set; } = "حاضر";
    public string? Notes { get; set; }
}
