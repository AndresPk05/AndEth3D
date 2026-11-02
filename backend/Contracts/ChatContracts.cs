using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Contracts;

public record SendChatMessageRequest(Guid RequestId, [Required, StringLength(2000, MinimumLength = 1)] string Message);