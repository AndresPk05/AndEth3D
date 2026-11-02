using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Http;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AndEth3D.Backend.Controllers;
[ApiController, Route("api/auth")]
public class AuthController(AuthService service) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request) => (await service.Register(request)).ToHttp(this);
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request) => (await service.Login(request)).ToHttp(this);
    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me() => (await service.Me()).ToHttp(this);
    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout() => (await service.Logout()).ToHttp(this);
}