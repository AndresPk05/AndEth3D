namespace AndEth3D.Backend.Http;
public static class GuestDesignAccess
{
    public static IApplicationBuilder UseGuestDesignAccess(this IApplicationBuilder app) => app.Use(async (context, next) => {
        if (context.User.IsInRole("Guest")) {
            var path = context.Request.Path;
            var allowed = path.StartsWithSegments("/api/chat") || path.StartsWithSegments("/api/estimation/conversations") ||
                path.StartsWithSegments("/api/parametric/conversations") || path.StartsWithSegments("/api/parametric/models");
            if (!allowed || path.Value!.EndsWith("/quotes", StringComparison.OrdinalIgnoreCase)) {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        await next();
    });
}
