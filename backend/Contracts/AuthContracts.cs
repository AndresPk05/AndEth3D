using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Contracts;

public record RegisterRequest([Required, StringLength(120)] string Name, [Required, EmailAddress, StringLength(254)] string Email, [Required, StringLength(72, MinimumLength = 12)] string Password);

public record LoginRequest([Required, EmailAddress, StringLength(254)] string Email, [Required, StringLength(72)] string Password);

public record AuthResponse(string Token, UserDto User);

public record UserDto(int Id, string Name, string Email, string Role);