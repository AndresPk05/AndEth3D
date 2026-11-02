using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController, Route("api/manufacturing")]
public class ManufacturingController(ManufacturingService service) : ControllerBase
{
    [HttpGet("catalog"), AllowAnonymous]
    public async Task<IActionResult> Catalog() => (await service.Catalog()).ToHttp(this);
    [HttpGet, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Get() => (await service.Get()).ToHttp(this);
    [HttpPut, Authorize(Roles = Roles.Admin), RequestSizeLimit(250_000)]
    public async Task<IActionResult> Save(SaveManufacturingRequest request) => (await service.Save(request)).ToHttp(this);
    [HttpGet("history"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> History() => (await service.History()).ToHttp(this);
    [HttpGet("history/{version:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Historical(int version) => (await service.Historical(version)).ToHttp(this);
    [HttpGet("quotes/{quoteId:int}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> QuoteConfiguration(int quoteId) => (await service.QuoteConfiguration(quoteId)).ToHttp(this);
}