namespace AndEth3D.Backend.Models;

public class QuoteRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public int? ManufacturingRevisionId { get; set; }
    public string? ReferenceFileUrl { get; set; }
    public string Status { get; set; } = QuoteStatus.Pending;
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class QuoteStatus
{
    public const string Pending = "Pending";
    public const string Quoted = "Quoted";
    public const string Converted = "Converted";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Pending, Quoted, Converted, Rejected];
}
