using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Contracts;

public record CreateVisualProposalRequest(Guid RequestId, [Required, RegularExpression("^[A-F0-9]{64}$")] string ExpectedFingerprint, Guid? ParentProposalId, bool AutoPreview = false);