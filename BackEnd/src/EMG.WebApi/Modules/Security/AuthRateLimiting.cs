using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace EMG.Api.Modules.Security;

public static class AuthRateLimiting
{
    public const string PolicyName = "auth-sensitive";

    public static RateLimitPartition<string> Partition(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
}
