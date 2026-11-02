using AndEth3D.Backend.Contracts;
using System.Text;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Services;
public class AuthService(AppDbContext db, ITokenIssuer tokens, IPasswordHasher passwords, ICurrentActor actor)
{
    public async Task<AppResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(email) || request.Password.Length < 12 || Encoding.UTF8.GetByteCount(request.Password) > 72)
            return AppResult.Invalid(new { message = "Nombre, email válido y contraseña de al menos 12 caracteres son obligatorios." });

        if (await db.Users.AnyAsync(u => u.Email == email))
            return AppResult.Conflict(new { message = "Ya existe una cuenta con ese email." });

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = passwords.Hash(request.Password),
            Role = Roles.Client,
        };

        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_Users_Email" })
        { return AppResult.Conflict(new { message = "Ya existe una cuenta con ese email." }); }

        return AppResult.Ok(new AuthResponse(tokens.CreateToken(user), new UserDto(user.Id, user.Name, user.Email, user.Role)));
    }
    public async Task<AppResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
            return AppResult.Unauthorized(new { message = "Email o contraseña incorrectos." });

        return AppResult.Ok(new AuthResponse(tokens.CreateToken(user), new UserDto(user.Id, user.Name, user.Email, user.Role)));
    }
    public async Task<AppResult> Me()
    {
        var id = actor.Id;
        var user = await db.Users.FindAsync(id);
        if (user is null) return AppResult.NotFound();
        return AppResult.Ok(new UserDto(user.Id, user.Name, user.Email, user.Role));
    }
    public async Task<AppResult> Logout()
    {
        var id = actor.Id;
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SessionVersion, u => u.SessionVersion + 1));
        return AppResult.NoContent();
    }
}
