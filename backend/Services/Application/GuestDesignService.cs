using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AndEth3D.Backend.Services;
public class GuestDesignOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxSessionsPerDay { get; set; } = 10;
}
public class GuestDesignService(AppDbContext db, ITokenIssuer tokens, IOptions<ChatOptions> chat, IOptions<GuestDesignOptions> options)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public async Task<AppResult> Create()
    {
        if (!options.Value.Enabled || !chat.Value.Enabled) return AppResult.Unavailable(new { message = "El asistente no está disponible. Puedes cotizar desde el formulario." });
        await Gate.WaitAsync();
        try {
            var since = DateTime.UtcNow.AddDays(-1);
            if (await db.Users.CountAsync(u => u.Role == "Guest" && u.CreatedAt >= since) >= options.Value.MaxSessionsPerDay)
                return AppResult.RateLimited(new { message = "El asistente alcanzó su límite de visitas. Puedes enviar tu idea desde Cotizar." });
            var user = new User { Name = "Visitante", Email = $"{Guid.NewGuid():N}@guest.invalid", PasswordHash = "", Role = "Guest" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return AppResult.Ok(new AuthResponse(tokens.CreateToken(user), new UserDto(user.Id, user.Name, "", user.Role)));
        }
        finally { Gate.Release(); }
    }
}
