using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Contracts;

public record OrderDto(
        int Id, int? UserId, string UserName, string UserEmail,
        string Title, string Description, decimal? Price,
        string Status, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

public record UpdateOrderStatusRequest([Required] string Status, [StringLength(5000)] string? Notes, [Range(typeof(decimal), "0", "9999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal? Price);