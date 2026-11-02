using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Npgsql;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Controllers;

[ApiController]
[Route("api/quotes")]
public class QuotesController(AppDbContext db, FileStorage storage) : ControllerBase
{
    public record QuoteDto(
        int Id, int UserId, string UserName, string UserEmail,
        string Description, string Material, string? ReferenceFileUrl,
        string Status, string? AdminNotes, DateTime CreatedAt);

    private static QuoteDto ToDto(QuoteRequest q) => new(
        q.Id, q.UserId, q.User.Name, q.User.Email,
        q.Description, q.Material, q.ReferenceFileUrl,
        q.Status, q.AdminNotes, q.CreatedAt);

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize]
    [HttpPost]
    [RequestSizeLimit(21_000_000)]
    public async Task<ActionResult<QuoteDto>> Create(
        [FromForm, Required, StringLength(5000)] string description,
        [FromForm, Required, StringLength(100)] string material,
        [FromForm] IFormFile? referenceFile)
    {
        var revision = await db.ManufacturingRevisions.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync();
        if (material != "No estoy seguro")
        {
            var selected = revision is null ? null : ManufacturingController.Read(revision).AvailableMaterials()
                .FirstOrDefault(m => string.Equals(m.Name, material, StringComparison.Ordinal));
            if (selected is null) return BadRequest(new { message = "El material ya no está disponible. Actualice el catálogo o solicite asesoría." });
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

        return Created($"/api/quotes/{quote.Id}", ToDto(quote));
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<QuoteDto>>> Mine()
    {
        var quotes = await db.QuoteRequests
            .Include(q => q.User)
            .Where(q => q.UserId == CurrentUserId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
        return Ok(quotes.Select(ToDto));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<ActionResult<List<QuoteDto>>> GetAll()
    {
        var quotes = await db.QuoteRequests
            .Include(q => q.User)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
        return Ok(quotes.Select(ToDto));
    }

    public record UpdateQuoteRequest([property: Required] string Status, [property: StringLength(5000)] string? AdminNotes);

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<QuoteDto>> Update(int id, UpdateQuoteRequest request)
    {
        if (!QuoteStatus.All.Contains(request.Status))
            return BadRequest(new { message = $"Estado inválido. Válidos: {string.Join(", ", QuoteStatus.All)}" });

        var quote = await db.QuoteRequests.Include(q => q.User).FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return NotFound();

        if (quote.Status == QuoteStatus.Converted || request.Status == QuoteStatus.Converted ||
            await db.Orders.AnyAsync(o => o.QuoteRequestId == id))
            return Conflict(new { message = "Una cotizacion convertida no puede cambiar de estado. Use convertir para crear un pedido." });
        quote.Status = request.Status;
        quote.AdminNotes = request.AdminNotes;
        await db.SaveChangesAsync();
        return Ok(ToDto(quote));
    }

    public record ConvertQuoteRequest([property: StringLength(200)] string? Title, [property: Range(typeof(decimal), "0", "9999999999.99")] decimal? Price, [property: StringLength(5000)] string? Notes);

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:int}/convert")]
    public async Task<ActionResult> Convert(int id, ConvertQuoteRequest request)
    {
        var quote = await db.QuoteRequests.Include(q => q.User).FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return NotFound();
        if (quote.Status == QuoteStatus.Converted || await db.Orders.AnyAsync(o => o.QuoteRequestId == id))
            return Conflict(new { message = "Esta cotización ya fue convertida en pedido." });

        if (quote.Status == QuoteStatus.Rejected)
            return Conflict(new { message = "Debe reabrir la cotizacion rechazada antes de convertirla." });
        var order = new Order
        {
            UserId = quote.UserId,
            QuoteRequestId = quote.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"Pedido de {quote.User.Name}" : request.Title,
            Description = quote.Description,
            Price = request.Price,
            Notes = request.Notes,
            Status = OrderStatus.Pending,
        };

        quote.Status = QuoteStatus.Converted;
        quote.AdminNotes = request.Notes ?? quote.AdminNotes;
        db.Orders.Add(order);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "IX_Orders_QuoteRequestId" })
        { return Conflict(new { message = "Esta cotizacion ya tiene un pedido." }); }
        return Ok(new { orderId = order.Id });
    }
    [Authorize]
    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> Download(int id)
    {
        var quote = await db.QuoteRequests.FindAsync(id);
        if (quote is null || (quote.UserId != CurrentUserId && !User.IsInRole(Roles.Admin))) return NotFound();
        var path = storage.ResolvePrivate(quote.ReferenceFileUrl);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
    }
}
