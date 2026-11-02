using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Contracts;

public record SaveManufacturingRequest([Range(0, int.MaxValue - 1)] int ExpectedVersion,
        [Required] ManufacturingConfiguration Configuration);