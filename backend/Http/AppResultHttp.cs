using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AndEth3D.Backend.Http;

public static class AppResultHttp
{
    public static IActionResult ToHttp(this AppResult result, ControllerBase controller, string? createdBase = null, string? acceptedLocation = null)
    {
        if (result.Outcome == AppOutcome.File) {
            if (result.Body is DownloadBytes bytes) {
                controller.Response.Headers.CacheControl = "private, no-store";
                controller.Response.Headers["X-Content-Type-Options"] = "nosniff";
                return controller.File(bytes.Bytes,bytes.ContentType,bytes.Name);
            }
            var file = (DownloadFile)result.Body!;
            if (file.Private) controller.Response.Headers.CacheControl = "private, no-store";
            controller.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return file.Name is null ? controller.PhysicalFile(file.Path, file.ContentType) : controller.PhysicalFile(file.Path, file.ContentType, file.Name);
        }
        if (result.Outcome == AppOutcome.Created && createdBase is not null)
            return controller.Created($"{createdBase}/{result.ResourceId}", result.Body);
        if (result.Outcome == AppOutcome.Accepted && acceptedLocation is not null)
            return controller.Accepted(acceptedLocation, result.Body);
        var status = result.Outcome switch {
            AppOutcome.Success => 200, AppOutcome.Created => 201, AppOutcome.Accepted => 202,
            AppOutcome.NoContent => 204, AppOutcome.Invalid => 400, AppOutcome.Unauthorized => 401,
            AppOutcome.Forbidden => 403, AppOutcome.NotFound => 404, AppOutcome.Conflict => 409,
            AppOutcome.RateLimited => 429, AppOutcome.Unavailable => 503,
            _ => throw new InvalidOperationException("Unsupported application result.")
        };
        return result.Body is null ? controller.StatusCode(status) : controller.StatusCode(status, result.Body);
    }
}
