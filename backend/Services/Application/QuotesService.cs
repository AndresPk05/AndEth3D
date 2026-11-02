using AndEth3D.Backend.Contracts;
using Npgsql;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Services;
public class QuotesService(AppDbContext db, FileStorage storage, ICurrentActor actor)
{

    private static QuoteDto ToDto(QuoteRequest q) => new(
        q.Id, q.UserId, q.User.Name, q.User.Email,
        q.Description, q.Material, q.ReferenceFileUrl,
        q.Status, q.AdminNotes, q.CreatedAt, q.DesignSnapshotJson is not null, q.ConfirmedPriceCop);

    private int CurrentUserId => actor.Id;
    public async Task<AppResult> Create(
        string description,
        string material,
        IFormFile? referenceFile)
    {
        var revision = await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        if (material != "No estoy seguro")
        {
            var selected = revision is null ? null : ManufacturingService.Read(revision).AvailableMaterials()
                .FirstOrDefault(m => string.Equals(m.Name, material, StringComparison.Ordinal));
            if (selected is null) return AppResult.Invalid(new { message = "El material ya no está disponible. Actualice el catálogo o solicite asesoría." });
            material = selected.Name;
        }
        var quote = new QuoteRequest
        {
            UserId = CurrentUserId,
            Description = description,
            Material = material,
            ManufacturingRevisionId = revision?.Id,
        };

        if (referenceFile is not null)
            quote.ReferenceFileUrl = await storage.SaveAsync(referenceFile, privateFile: true);

        db.QuoteRequests.Add(quote);
        await db.SaveChangesAsync();
        await db.Entry(quote).Reference(q => q.User).LoadAsync();

        return AppResult.Created(quote.Id.ToString(), ToDto(quote));
    }
    public async Task<AppResult> Mine()
    {
        var quotes = await db.QuoteRequests
            .Include(q => q.User)
            .Where(q => q.UserId == CurrentUserId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
        return AppResult.Ok(quotes.Select(ToDto));
    }
    public async Task<AppResult> GetAll()
    {
        var quotes = await db.QuoteRequests
            .Include(q => q.User)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
        return AppResult.Ok(quotes.Select(ToDto));
    }
    public async Task<AppResult> Update(int id, UpdateQuoteRequest request)
    {
        if (!QuoteStatus.All.Contains(request.Status))
            return AppResult.Invalid(new { message = $"Estado inválido. Válidos: {string.Join(", ", QuoteStatus.All)}" });

        var quote = await db.QuoteRequests.Include(q => q.User).FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return AppResult.NotFound();

        if (quote.DesignSnapshotJson is not null && request.Status == QuoteStatus.Quoted)
            return AppResult.Invalid(new { message = "Confirma el precio desde la revisión de la propuesta." });

        if (quote.Status == QuoteStatus.Converted || request.Status == QuoteStatus.Converted ||
            await db.Orders.AnyAsync(o => o.QuoteRequestId == id))
            return AppResult.Conflict(new { message = "Una cotizacion convertida no puede cambiar de estado. Use convertir para crear un pedido." });
        quote.Status = request.Status;
        quote.AdminNotes = request.AdminNotes;
        if (quote.DesignSnapshotJson is not null) quote.ConfirmedPriceCop = null;
        quote.ReviewVersion++;
        await db.SaveChangesAsync();
        return AppResult.Ok(ToDto(quote));
    }
    public async Task<AppResult> Convert(int id, ConvertQuoteRequest request)
    {
        var quote = await db.QuoteRequests.Include(q => q.User).FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return AppResult.NotFound();
        if (quote.Status == QuoteStatus.Converted || await db.Orders.AnyAsync(o => o.QuoteRequestId == id))
            return AppResult.Conflict(new { message = "Esta cotización ya fue convertida en pedido." });

        if (quote.Status == QuoteStatus.Rejected)
            return AppResult.Conflict(new { message = "Debe reabrir la cotizacion rechazada antes de convertirla." });
        if (quote.DesignSnapshotJson is not null && (quote.ConfirmedPriceCop is null || quote.Status != QuoteStatus.Quoted || (request.Price is not null && request.Price != quote.ConfirmedPriceCop)))
            return AppResult.Conflict(new { message = "Revisa y confirma el precio de la propuesta antes de convertirla. El pedido debe conservar ese precio." });
        var order = new Order
        {
            UserId = quote.UserId,
            QuoteRequestId = quote.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"Pedido de {quote.User.Name}" : request.Title,
            Description = quote.Description,
            Price = quote.DesignSnapshotJson is not null ? quote.ConfirmedPriceCop : request.Price,
            Notes = request.Notes,
            Status = OrderStatus.Pending,
        };

        quote.Status = QuoteStatus.Converted;
        quote.ReviewVersion++;
        quote.AdminNotes = request.Notes ?? quote.AdminNotes;
        db.Orders.Add(order);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "IX_Orders_QuoteRequestId" })
        { return AppResult.Conflict(new { message = "Esta cotizacion ya tiene un pedido." }); }
        return AppResult.Ok(new { orderId = order.Id });
    }
    public async Task<AppResult> Download(int id)
    {
        var quote = await db.QuoteRequests.FindAsync(id);
        if (quote is null || (quote.UserId != CurrentUserId && !actor.IsAdmin)) return AppResult.NotFound();
        var path = storage.ResolvePrivate(quote.ReferenceFileUrl);
        if (path is null || !System.IO.File.Exists(path)) return AppResult.NotFound();
        return AppResult.File(new DownloadFile(path, "application/octet-stream", Path.GetFileName(path)));
    }
}
