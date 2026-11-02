using System.ComponentModel.DataAnnotations;
namespace AndEth3D.Backend.Models;

public class CalibrationRevision
{
    public int Id { get; set; }
    public int Version { get; set; }
    public int ManufacturingRevisionId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string SamplesJson { get; set; } = "[]";
}

// One observed, sliced or manufactured piece; grams include supports, purge and waste.
public class CalibrationSample
{
    [Required, StringLength(60)] public string CategoryId { get; set; } = "";
    [Required, StringLength(60)] public string MaterialId { get; set; } = "";
    [Required, StringLength(60)] public string PrinterId { get; set; } = "";
    [Required, RegularExpression("^(simple|standard|complex)$")] public string Complexity { get; set; } = "standard";
    [Range(1,16)] public int ColorCount { get; set; } = 1;
    [Range(1,5000)] public decimal WidthMm { get; set; }
    [Range(1,5000)] public decimal DepthMm { get; set; }
    [Range(1,5000)] public decimal HeightMm { get; set; }
    [Range(0.01,100000)] public decimal Grams { get; set; }
    [Range(0.01,10000)] public decimal MachineHours { get; set; }
    [Range(0,1000)] public decimal DesignHours { get; set; }
    [Range(0,1000)] public decimal FinishingHours { get; set; }
    [Required, StringLength(500, MinimumLength=3)] public string Evidence { get; set; } = "";
}
