using System.ComponentModel.DataAnnotations;

namespace AndEth3D.Backend.Models;

// Revisions are append-only. Quotes retain the revision used at submission.
public class ManufacturingRevision
{
    public int Id { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
}

public class ManufacturingConfiguration : IValidatableObject
{
    [Required, MaxLength(100)] public List<PrinterProfile> Printers { get; set; } = [];
    [Required, MaxLength(200)] public List<MaterialProfile> Materials { get; set; } = [];
    [Required, MaxLength(100)] public List<ProductCategory> Categories { get; set; } = [];
    [Required] public PricingProfile Pricing { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Printers is null || Materials is null || Categories is null) yield break;
        if (Printers.Any(p => p is null) || Materials.Any(m => m is null) || Categories.Any(c => c is null))
        { yield return new("Las listas no pueden contener elementos vacíos."); yield break; }
        if (Materials.Select(m => m.Id).Distinct().Count() != Materials.Count ||
            Printers.Select(p => p.Id).Distinct().Count() != Printers.Count ||
            Categories.Select(c => c.Id).Distinct().Count() != Categories.Count)
            yield return new("Los identificadores deben ser únicos en cada catálogo.");
        if (Materials.Select(m => m.Name?.Trim().ToUpperInvariant()).Distinct().Count() != Materials.Count)
            yield return new("Los nombres de materiales deben ser únicos.");
        foreach (var p in Printers)
        {
            if (p.WidthMm <= 2 * p.MarginMm || p.DepthMm <= 2 * p.MarginMm || p.HeightMm <= p.MarginMm)
                yield return new("El margen debe dejar un volumen útil positivo.");
            if (p.MaterialIds is not null && p.MaterialIds.Any(id => !Materials.Any(m => m.Id == id)))
                yield return new("Una impresora referencia un material inexistente.");
            if (p.Active && (p.MaterialIds is null || p.MaterialIds.Count == 0))
                yield return new("Una impresora activa necesita materiales compatibles.");
        }
    }

    public IEnumerable<MaterialProfile> AvailableMaterials() => Materials.Where(m =>
        m.Active && m.Colors.Any(c => c.Available) && Printers.Any(p => p.Active && p.MaterialIds.Contains(m.Id)));
}

public class PrinterProfile
{
    [Required, RegularExpression(@"^[a-z0-9-]{1,60}$")] public string Id { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(100)] public string Brand { get; set; } = "";
    [Required, StringLength(100)] public string Model { get; set; } = "";
    [Required, RegularExpression("^FDM$")] public string Technology { get; set; } = "FDM";
    public bool Active { get; set; }
    [Range(1, 5000)] public decimal WidthMm { get; set; } = 256;
    [Range(1, 5000)] public decimal DepthMm { get; set; } = 256;
    [Range(1, 5000)] public decimal HeightMm { get; set; } = 256;
    [Range(0, 100)] public decimal MarginMm { get; set; } = 5;
    [Range(typeof(decimal), "0.1", "2", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal NozzleMm { get; set; } = 0.4m;
    [Range(typeof(decimal), "0.1", "20", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal MinimumWallMm { get; set; } = 1.2m;
    [Range(1, 16)] public int MaxColors { get; set; } = 1;
    [Required, MaxLength(200)] public List<string> MaterialIds { get; set; } = [];
}

public class MaterialProfile : IValidatableObject
{
    [Required, RegularExpression(@"^[a-z0-9-]{1,60}$")] public string Id { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public bool Active { get; set; }
    [Range(typeof(decimal), "0.01", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal CostPerKgCop { get; set; }
    [Required, MaxLength(100)] public List<MaterialColor> Colors { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Colors is null) yield break;
        if (Colors.Any(c => c is null)) { yield return new("Color vacío."); yield break; }
        if (Colors.Select(c => c.Name?.Trim().ToUpperInvariant()).Distinct().Count() != Colors.Count)
            yield return new("No repita colores dentro del material.");
    }
}
public class MaterialColor
{
    [Required, StringLength(60)] public string Name { get; set; } = "";
    public bool Available { get; set; }
}
public class ProductCategory
{
    [Required, RegularExpression(@"^[a-z0-9-]{1,60}$")] public string Id { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
}
public class PricingProfile
{
    [Range(typeof(decimal), "0", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal MachineHourCop { get; set; }
    [Range(typeof(decimal), "0", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal DesignHourCop { get; set; }
    [Range(typeof(decimal), "0", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal FinishingHourCop { get; set; }
    [Range(typeof(decimal), "0", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal MinimumOrderCop { get; set; }
    [Range(typeof(decimal), "0", "0.95", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal GrossMargin { get; set; }
}
