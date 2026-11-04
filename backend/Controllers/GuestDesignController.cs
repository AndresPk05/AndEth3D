using AndEth3D.Backend.Http;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AndEth3D.Backend.Controllers;
[ApiController, Route("api/design-guest")]
public class GuestDesignController(GuestDesignService service) : ControllerBase
{
    [AllowAnonymous, HttpPost, EnableRateLimiting("guest-design"), RequestSizeLimit(1024)]
    public async Task<IActionResult> Create() => (await service.Create()).ToHttp(this);
}
