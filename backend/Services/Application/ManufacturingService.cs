using AndEth3D.Backend.Contracts;
using System.Text.Json;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AndEth3D.Backend.Services;
public class ManufacturingService(AppDbContext db, ICurrentActor actor)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal static ManufacturingConfiguration Read(ManufacturingRevision revision) =>
        JsonSerializer.Deserialize<ManufacturingConfiguration>(revision.ConfigurationJson, JsonOptions)!;
    public async Task<AppResult> Catalog()
    {
        var revision = await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        var config = revision is null ? new ManufacturingConfiguration() : Read(revision);
        // Explicit allowlist: no machine identifiers, names, internal costs or rates.
        return AppResult.Ok(new {
            materials = config.AvailableMaterials().Select(m => new {
                id = m.Id, name = m.Name, colors = m.Colors.Where(c => c.Available).Select(c => c.Name)
            }),
            categories = config.Categories.Where(c => c.Active).Select(c => new { id = c.Id, name = c.Name })
        });
    }
    public async Task<AppResult> Get()
    {
        var revision = await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        return AppResult.Ok(new { version = revision?.Version ?? 0, configuration = revision is null ? Defaults() : Read(revision) });
    }
    public async Task<AppResult> Save(SaveManufacturingRequest request)
    {
        var version = await db.ManufacturingRevisions.MaxAsync(r => (int?)r.Version) ?? 0;
        if (version != request.ExpectedVersion)
            return AppResult.Conflict(new { message = "La configuración cambió. Recargue antes de guardar." });
        var revision = new ManufacturingRevision {
            Version = version + 1,
            CreatedByUserId = actor.Id,
            ConfigurationJson = JsonSerializer.Serialize(request.Configuration, JsonOptions)
        };
        db.ManufacturingRevisions.Add(revision);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { return AppResult.Conflict(new { message = "Otro administrador guardó cambios. Recargue antes de continuar." }); }
        return AppResult.Ok(new { version = revision.Version, configuration = request.Configuration });
    }
    public async Task<AppResult> History() => AppResult.Ok(await db.ManufacturingRevisions.AsNoTracking()
        .OrderByDescending(r => r.Version).Take(100)
        .Select(r => new { r.Version, r.CreatedAt, r.CreatedByUserId }).ToListAsync());
    public async Task<AppResult> Historical(int version)
    {
        var revision = await db.ManufacturingRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Version == version);
        return revision is null ? AppResult.NotFound() : AppResult.Ok(new { revision.Version, configuration = Read(revision) });
    }
    public async Task<AppResult> QuoteConfiguration(int quoteId)
    {
        var quote = await db.QuoteRequests.AsNoTracking().SingleOrDefaultAsync(q => q.Id == quoteId);
        if (quote is null) return AppResult.NotFound();
        if (quote.ManufacturingRevisionId is null)
            return AppResult.Ok(new { version = (int?)null, message = "Cotización anterior a la configuración de fabricación." });
        var revision = await db.ManufacturingRevisions.AsNoTracking().SingleAsync(r => r.Id == quote.ManufacturingRevisionId);
        return AppResult.Ok(new { revision.Version, configuration = Read(revision) });
    }

    private static ManufacturingConfiguration Defaults() => new() {
        Printers = [new() { Id = "a1", Name = "Equipo principal", Brand = "Bambu Lab", Model = "A1", MaterialIds = ["pla", "petg"] }],
        Materials = [new() { Id = "pla", Name = "PLA" }, new() { Id = "petg", Name = "PETG" }],
        Categories = new[] { ("cajas", "Cajas"), ("organizadores", "Organizadores"), ("llaveros", "Llaveros"),
            ("placas", "Placas"), ("decoracion", "Decoración sencilla") }
            .Select(c => new ProductCategory { Id = c.Item1, Name = c.Item2 }).ToList()
    };
}
