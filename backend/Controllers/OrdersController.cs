using System.Security.Claims;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Controllers;

[ApiController]
[Route("api")]
public class OrdersController(AppDbContext db) : ControllerBase
{
    public record OrderDto(
        int Id, int UserId, string UserName, string UserEmail,
        string Title, string Description, decimal? Price,
        string Status, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.UserId, o.User.Name, o.User.Email,
        o.Title, o.Description, o.Price,
        o.Status, o.Notes, o.CreatedAt, o.UpdatedAt);

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize]
    [HttpGet("orders")]
    public async Task<ActionResult<List<OrderDto>>> Mine()
    {
        var orders = await db.Orders
            .Include(o => o.User)
            .Where(o => o.UserId == CurrentUserId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return Ok(orders.Select(ToDto));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("admin/orders")]
    public async Task<ActionResult<List<OrderDto>>> GetAll()
    {
        var orders = await db.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return Ok(orders.Select(ToDto));
    }

    public record UpdateOrderStatusRequest(string Status, string? Notes, decimal? Price);

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("admin/orders/{id:int}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, UpdateOrderStatusRequest request)
    {
        if (!OrderStatus.All.Contains(request.Status))
            return BadRequest(new { message = $"Estado inválido. Válidos: {string.Join(", ", OrderStatus.All)}" });

        var order = await db.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        order.Status = request.Status;
        order.Notes = request.Notes;
        order.Price = request.Price ?? order.Price;
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(order));
    }
}
