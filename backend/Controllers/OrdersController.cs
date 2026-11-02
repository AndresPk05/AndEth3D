using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController, Route("api")]
public class OrdersController(OrdersService service) : ControllerBase
{
    [Authorize, HttpGet("orders")]
    public async Task<IActionResult> Mine() => (await service.Mine()).ToHttp(this);
    [Authorize(Roles = Roles.Admin), HttpGet("admin/orders")]
    public async Task<IActionResult> GetAll() => (await service.GetAll()).ToHttp(this);
    [Authorize(Roles = Roles.Admin), HttpPut("admin/orders/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusRequest request) => (await service.UpdateStatus(id, request)).ToHttp(this);
}