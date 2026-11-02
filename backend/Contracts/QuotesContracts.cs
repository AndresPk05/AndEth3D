using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Contracts;

public record QuoteDto(
        int Id, int UserId, string UserName, string UserEmail,
        string Description, string Material, string? ReferenceFileUrl,
        string Status, string? AdminNotes, DateTime CreatedAt, bool FromDesign = false, decimal? ConfirmedPriceCop = null);

public record UpdateQuoteRequest([Required] string Status, [StringLength(5000)] string? AdminNotes);

public record ConvertQuoteRequest([StringLength(200)] string? Title, [Range(typeof(decimal), "0", "9999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal? Price, [StringLength(5000)] string? Notes);
