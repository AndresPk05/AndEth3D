using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AndEth3D.Backend.Controllers;

[ApiController, Authorize(Roles = Roles.Admin), Route("api/admin/users")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminUsersController(AuthService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(0, int.MaxValue)] int afterId = 0) =>
        (await service.ListUsers(afterId)).ToHttp(this);

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request) =>
        (await service.CreateUser(request)).ToHttp(this);
}
