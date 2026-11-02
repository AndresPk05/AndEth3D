using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AndEth3D.Backend.Http;
public static class GuestQuoteRateLimits
{
    // Global per-process cap also works behind a proxy without trusting forwarded IP headers.
    public static IServiceCollection AddGuestQuoteRateLimits(this IServiceCollection services) => services.AddRateLimiter(o => {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddFixedWindowLimiter("guest-quotes", limit => {
            limit.PermitLimit = 60;
            limit.Window = TimeSpan.FromHours(1);
            limit.QueueLimit = 0;
        });
    });
}
