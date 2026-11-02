using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace AndEth3D.Backend.Services;
public class OrdersService(AppDbContext db, ICurrentActor actor)
{

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.UserId, o.User.Name, o.User.Email,
        o.Title, o.Description, o.Price,
        o.Status, o.Notes, o.CreatedAt, o.UpdatedAt);

    private int CurrentUserId => actor.Id;
    public async Task<AppResult> Mine()
    {
        var orders = await db.Orders
            .Include(o => o.User)
            .Where(o => o.UserId == CurrentUserId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return AppResult.Ok(orders.Select(ToDto));
    }
    public async Task<AppResult> GetAll()
    {
        var orders = await db.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return AppResult.Ok(orders.Select(ToDto));
    }
    public async Task<AppResult> UpdateStatus(int id, UpdateOrderStatusRequest request)
    {
        if (!OrderStatus.All.Contains(request.Status))
            return AppResult.Invalid(new { message = $"Estado inválido. Válidos: {string.Join(", ", OrderStatus.All)}" });

        var order = await db.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return AppResult.NotFound();
        if(request.Price is not null && request.Price!=order.Price && order.QuoteRequestId is not null &&
            await db.QuoteRequests.AnyAsync(q=>q.Id==order.QuoteRequestId&&q.DesignSnapshotJson!=null))
            return AppResult.Conflict(new{message="Este pedido conserva el precio confirmado en su cotización."});

        if (!OrderStatus.CanTransition(order.Status, request.Status))
            return AppResult.Conflict(new { message = "Transicion de estado no permitida." });
        order.Status = request.Status;
        order.Notes = request.Notes;
        order.Price = request.Price ?? order.Price;
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return AppResult.Ok(ToDto(order));
    }
}
