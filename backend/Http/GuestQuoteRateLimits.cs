using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AndEth3D.Backend.Http;
public static class GuestQuoteRateLimits
{
    // Global per-process cap also works behind a proxy without trusting forwarded IP headers.
    public static IServiceCollection AddGuestQuoteRateLimits(this IServiceCollection services) => services.AddRateLimiter(o => {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        foreach (var (name, count, minutes) in new[] { ("register", 5, 60), ("login", 15, 10), ("guest-design", 3, 60) })
        {
            o.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = count, Window = TimeSpan.FromMinutes(minutes), QueueLimit = 0 }));
        }
        o.AddFixedWindowLimiter("guest-quotes", limit => {
            limit.PermitLimit = 60;
            limit.Window = TimeSpan.FromHours(1);
            limit.QueueLimit = 0;
        });
    });
}
