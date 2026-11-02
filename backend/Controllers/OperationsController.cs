using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController,Authorize(Roles=Roles.Admin),Route("api/operations")]
public class OperationsController(OperationsService service):ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery,Range(1,365)] int days=30)=>(await service.Dashboard(days)).ToHttp(this);
    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> Context(int id)=>(await service.Context(id)).ToHttp(this);
    [HttpPost("orders/{id:int}/production"),RequestSizeLimit(16000)]
    public async Task<IActionResult> Record(int id,RecordProductionRequest request)=>(await service.Record(id,request)).ToHttp(this);
    [HttpGet("calibration")]
    public async Task<IActionResult> Calibration()=>(await service.Calibration()).ToHttp(this);
    [HttpPost("calibration"),RequestSizeLimit(16000)]
    public async Task<IActionResult> Promote(PromoteProductionRequest request)=>(await service.Promote(request)).ToHttp(this);
}
