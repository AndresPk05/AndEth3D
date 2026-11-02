using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController, Authorize(Roles = Roles.Admin), Route("api/visual-settings")]
public class VisualSettingsController(VisualSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => (await service.Get()).ToHttp(this);
    [HttpPut, RequestSizeLimit(16000)]
    public async Task<IActionResult> Save(SaveVisualSettingsRequest request) => (await service.Save(request)).ToHttp(this);
    [HttpGet("history")]
    public async Task<IActionResult> History() => (await service.History()).ToHttp(this);
    [HttpGet("usage")]
    public async Task<IActionResult> Usage() => (await service.Usage()).ToHttp(this);
}