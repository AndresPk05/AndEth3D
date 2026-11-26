using AndEth3D.Backend.Contracts;
using System.Text;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Services;
public class AuthService(AppDbContext db, ITokenIssuer tokens, IPasswordHasher passwords, ICurrentActor actor)
{
    private const int UsersPageSize = 25;

    public async Task<AppResult> ListUsers(int afterId)
    {
        if (!actor.IsAdmin) return new(AppOutcome.Forbidden);

        var users = await db.Users.AsNoTracking()
            .Where(u => u.Id > afterId && (u.Role == Roles.Admin || u.Role == Roles.Client))
            .OrderBy(u => u.Id)
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.Role))
            .Take(UsersPageSize + 1)
            .ToListAsync();

        int? nextAfterId = users.Count > UsersPageSize ? users[UsersPageSize - 1].Id : null;
        return AppResult.Ok(new UsersPage(users.Take(UsersPageSize).ToArray(), nextAfterId));
    }

    public async Task<AppResult> CreateUser(CreateUserRequest request)
    {
        if (!actor.IsAdmin) return new(AppOutcome.Forbidden);
        if (request.Role is not (Roles.Admin or Roles.Client))
            return AppResult.Invalid(new { message = "Selecciona un rol válido." });

        var email = request.Email.Trim().ToLowerInvariant();
        if (!ValidCredentials(request.Name, email, request.Password))
            return AppResult.Invalid(new { message = "Ingresa un nombre, un correo válido y una contraseña de al menos 12 caracteres y hasta 72 bytes." });

        var duplicate = AppResult.Conflict(new { message = "Ya existe una cuenta con ese correo." });
        if (await db.Users.AnyAsync(u => u.Email == email))
            return duplicate;

        var user = await AddUser(request.Name, email, passwords.Hash(request.Password), request.Role);
        return user is null
            ? duplicate
            : AppResult.Created(user.Id.ToString(), new UserDto(user.Id, user.Name, user.Email, user.Role));
    }

    public async Task<AppResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (!ValidCredentials(request.Name, email, request.Password))
            return AppResult.Invalid(new { message = "Nombre, email vÃƒÂ¡lido y contraseÃƒÂ±a de al menos 12 caracteres son obligatorios." });

        var receipt = AppResult.Ok(new { message = "ContinÃƒÂºa al inicio de sesiÃƒÂ³n." });
        var hash = passwords.Hash(request.Password);
        if (await db.Users.AnyAsync(u => u.Email == email))
            return receipt;

        await AddUser(request.Name, email, hash, Roles.Client);
        return receipt;
    }

    private static bool ValidCredentials(string name, string email, string password) =>
        !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email)
        && password.Length >= 12 && Encoding.UTF8.GetByteCount(password) <= 72;

    private async Task<User?> AddUser(string name, string email, string hash, string role)
    {
        var user = new User
        {
            Name = name.Trim(),
            Email = email,
            PasswordHash = hash,
            Role = role,
        };

        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_Users_Email" })
        { return null; }

        return user;
    }
    public async Task<AppResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || user.Role == "Guest")
        {
            // Spend comparable password-hashing work for unknown and existing accounts.
            passwords.Hash(request.Password);
            return AppResult.Unauthorized(new { message = "Email o contraseña incorrectos." });
        }
        if (!passwords.Verify(request.Password, user.PasswordHash))
            return AppResult.Unauthorized(new { message = "Email o contraseÃƒÂ±a incorrectos." });

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
