using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController,Authorize,Route("api/parametric")]
public class ParametricController(ParametricService service):ControllerBase
{
    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> List(Guid id)=>(await service.List(id)).ToHttp(this);
    [HttpPost("conversations/{id:guid}"),RequestSizeLimit(8000)]
    public async Task<IActionResult> Create(Guid id,CreateParametricRequest request)=>(await service.Create(id,request)).ToHttp(this);
    [HttpGet("models/{id:guid}")]
    public async Task<IActionResult> Model(Guid id)=>(await service.Model(id)).ToHttp(this);
    [HttpPost("models/{id:guid}/quotes"), Authorize(Roles = Roles.Client + "," + Roles.Admin)]
    public async Task<IActionResult> Quote(Guid id,ParametricQuoteRequest request)=>(await service.Quote(id,request)).ToHttp(this);
    [HttpGet("admin"),Authorize(Roles=Roles.Admin)]
    public async Task<IActionResult> Admin()=>(await service.Admin()).ToHttp(this);
    [HttpPut("models/{id:guid}/measurement"),Authorize(Roles=Roles.Admin)]
    public async Task<IActionResult> Measure(Guid id,MeasureParametricRequest request)=>(await service.Measure(id,request)).ToHttp(this);
}
