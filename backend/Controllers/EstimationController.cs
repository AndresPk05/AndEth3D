using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
namespace AndEth3D.Backend.Controllers;
[ApiController, Authorize, Route("api/estimation")]
public class EstimationController(EstimationService service) : ControllerBase
{
    [HttpGet("calibration"), Authorize(Roles=Roles.Admin)]
    public async Task<IActionResult> Calibration() => (await service.Calibration()).ToHttp(this);
    [HttpPut("calibration"), Authorize(Roles=Roles.Admin), RequestSizeLimit(1000000)]
    public async Task<IActionResult> Save(SaveCalibrationRequest request) => (await service.Save(request)).ToHttp(this);
    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> Estimate(Guid id, [FromQuery, RegularExpression("^(simple|standard|complex)$")] string complexity = "standard") => (await service.Estimate(id, complexity)).ToHttp(this);
    [HttpPost("conversations/{id:guid}/quotes")]
    public async Task<IActionResult> Submit(Guid id, SubmitDesignQuoteRequest request) => (await service.Submit(id, request)).ToHttp(this);
    [HttpGet("quotes/{id:int}")]
    public async Task<IActionResult> Detail(int id) => (await service.Detail(id)).ToHttp(this);
    [HttpGet("quotes/{id:int}/image")]
    public async Task<IActionResult> Image(int id) => (await service.Image(id)).ToHttp(this);
    [HttpPut("quotes/{id:int}/review"), Authorize(Roles=Roles.Admin)]
    public async Task<IActionResult> Review(int id, ReviewDesignQuoteRequest request) => (await service.Review(id,request)).ToHttp(this);
}
