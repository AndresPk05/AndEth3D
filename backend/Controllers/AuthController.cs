using System.Security.Claims;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    public record RegisterRequest(string Name, string Email, string Password);
    public record LoginRequest(string Email, string Password);
    public record AuthResponse(string Token, UserDto User);
    public record UserDto(int Id, string Name, string Email, string Role);

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(email) || request.Password.Length < 6)
            return BadRequest(new { message = "Nombre, email válido y contraseña de al menos 6 caracteres son obligatorios." });

        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Ya existe una cuenta con ese email." });

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Roles.Client,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(tokens.CreateToken(user), new UserDto(user.Id, user.Name, user.Email, user.Role)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Email o contraseña incorrectos." });

        return Ok(new AuthResponse(tokens.CreateToken(user), new UserDto(user.Id, user.Name, user.Email, user.Role)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        return Ok(new UserDto(user.Id, user.Name, user.Email, user.Role));
    }
}
