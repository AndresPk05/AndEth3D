using System.Security.Claims;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;

namespace AndEth3D.Backend.Http;

public class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public int Id => int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new InvalidOperationException("An authenticated actor is required.");
    public bool IsAdmin => accessor.HttpContext?.User.IsInRole(Roles.Admin) == true;
}
