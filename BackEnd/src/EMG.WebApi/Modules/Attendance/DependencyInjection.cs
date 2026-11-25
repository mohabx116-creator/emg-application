using EMG.Api.Modules.Attendance.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Attendance;

public static class DependencyInjection
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceService, AttendanceService>();
        return services;
    }
}
