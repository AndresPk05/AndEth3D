namespace AndEth3D.Backend.Models;

public class Order
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public int? QuoteRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string InQueue = "InQueue";
    public const string Printing = "Printing";
    public const string Finished = "Finished";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";

    public static bool CanTransition(string from, string to) => from == to || (from, to) switch
    {
        (Pending, InQueue or Cancelled) => true,
        (InQueue, Printing or Cancelled) => true,
        (Printing, Finished or Cancelled) => true,
        (Finished, Delivered or Cancelled) => true,
        _ => false
    };

    public static readonly string[] All = [Pending, InQueue, Printing, Finished, Delivered, Cancelled];
}
