using System.Security.Claims;
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
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<QuoteDto>> Create(
        [FromForm] string description,
        [FromForm] string material,
        [FromForm] IFormFile? referenceFile)
    {
        var quote = new QuoteRequest
        {
            UserId = CurrentUserId,
            Description = description,
            Material = material,
        };

        if (referenceFile is not null)
            quote.ReferenceFileUrl = await storage.SaveAsync(referenceFile);

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

    public record UpdateQuoteRequest(string Status, string? AdminNotes);

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<QuoteDto>> Update(int id, UpdateQuoteRequest request)
    {
        if (!QuoteStatus.All.Contains(request.Status))
            return BadRequest(new { message = $"Estado inválido. Válidos: {string.Join(", ", QuoteStatus.All)}" });

        var quote = await db.QuoteRequests.Include(q => q.User).FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return NotFound();

        quote.Status = request.Status;
        quote.AdminNotes = request.AdminNotes;
        await db.SaveChangesAsync();
        return Ok(ToDto(quote));
    }

    public record ConvertQuoteRequest(string Title, decimal? Price, string? Notes);

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:int}/convert")]
    public async Task<ActionResult> Convert(int id, ConvertQuoteRequest request)
    {
        var quote = await db.QuoteRequests.Include(q => q.User).FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return NotFound();
        if (quote.Status == QuoteStatus.Converted)
            return Conflict(new { message = "Esta cotización ya fue convertida en pedido." });

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
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return Ok(new { orderId = order.Id });
    }
}
