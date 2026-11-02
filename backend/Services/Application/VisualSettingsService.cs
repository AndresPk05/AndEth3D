using AndEth3D.Backend.Contracts;
using System.Text.Json;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AndEth3D.Backend.Services;
public class VisualSettingsService(AppDbContext db, IOptions<ChatOptions> credentials, ICurrentActor actor)
{
    public async Task<AppResult> Get()
    {
        var latest = await db.VisualSettingsRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        return AppResult.Ok(new { version = latest?.Version ?? 0, configuration = latest is null ? new() : VisualPolicy.Settings(latest),
            keyConfigured = !string.IsNullOrWhiteSpace(credentials.Value.ApiKey) });
    }
    public async Task<AppResult> Save(SaveVisualSettingsRequest request)
    {
        if (request.Configuration.Enabled && string.IsNullOrWhiteSpace(credentials.Value.ApiKey))
            return AppResult.Invalid(new { message = "Configura primero la clave privada de OpenRouter en el servidor." });
        var version = await db.VisualSettingsRevisions.MaxAsync(r => (int?)r.Version) ?? 0;
        if (version != request.ExpectedVersion) return AppResult.Conflict(new { message = "La configuración cambió. Recarga antes de guardar." });
        var revision = new VisualSettingsRevision { Version = version + 1,
            CreatedByUserId = actor.Id,
            ConfigurationJson = JsonSerializer.Serialize(request.Configuration, DesignPolicy.Json) };
        db.VisualSettingsRevisions.Add(revision);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { return AppResult.Conflict(new { message = "Otro administrador guardó cambios. Recarga antes de continuar." }); }
        return AppResult.Ok(new { version = revision.Version });
    }
    public async Task<AppResult> History() => AppResult.Ok(await db.VisualSettingsRevisions.AsNoTracking().OrderByDescending(r => r.Version)
        .Take(50).Select(r => new { r.Version, r.CreatedAt, r.CreatedByUserId }).ToListAsync());
    public async Task<AppResult> Usage() => AppResult.Ok((await db.VisualProposals.AsNoTracking().OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync())
        .Select(p => new { p.Id, p.ConversationId, p.Number, p.Status, p.CreatedAt, p.SettingsRevisionId, p.ReservedUsd,
            attempts = VisualPolicy.Attempts(p) }));
}
