using System.ComponentModel.DataAnnotations;

namespace AndEth3D.Backend.Contracts;
public class GuestQuoteRequest
{
    [Required, StringLength(100)] public string ContactName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string ContactEmail { get; set; } = "";
    [Required, StringLength(5000)] public string Description { get; set; } = "";
    [Required, StringLength(100)] public string Material { get; set; } = "";
    public IFormFile? ReferenceFile { get; set; }
}
