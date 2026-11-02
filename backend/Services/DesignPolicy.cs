using System.Text.Json;
using System.Text.Json.Serialization;
using AndEth3D.Backend.Models;

namespace AndEth3D.Backend.Services;

public record DesignAssessment(string Status, string Question, string[] Suggestions, string[] Issues, string[] Missing);

public static class DesignPolicy
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static readonly string[] Fields = ["object", "use", "categoryId", "widthMm", "depthMm", "heightMm", "quantity", "materialId", "colors", "personalization", "reference"];
    public static DesignBrief Read(string json) => JsonSerializer.Deserialize<DesignBrief>(json, Json)!;
    public static string Write(DesignBrief brief) => JsonSerializer.Serialize(brief, Json);

    public static object PublicCapabilities(ManufacturingConfiguration config) => new {
        materials = config.AvailableMaterials().Select(m => new { m.Id, m.Name, colors = m.Colors.Where(c => c.Available).Select(c => c.Name) }),
        categories = config.Categories.Where(c => c.Active).Select(c => new { c.Id, c.Name }),
        // Each alternative stays separate: never mix dimensions/capabilities across machines.
        options = config.Printers.Where(p => p.Active).Select(p => new {
            widthMm = p.WidthMm - 2 * p.MarginMm, depthMm = p.DepthMm - 2 * p.MarginMm,
            heightMm = p.HeightMm - p.MarginMm, p.MaxColors, p.MinimumWallMm, p.MaterialIds
        })
    };

    public static DesignBrief Merge(DesignBrief before, DesignPatch patch)
    {
        if (patch.ClearFields is null || patch.ClearFields.Any(f => !Fields.Contains(f)) ||
            patch.Colors?.Count > 16 || patch.Colors?.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 60) == true ||
            new[] { patch.Object, patch.Use, patch.Personalization, patch.Reference }.Any(s => s?.Length > 1000) ||
            patch.CategoryId?.Length > 60 || patch.MaterialId?.Length > 60 ||
            new[] { patch.WidthMm, patch.DepthMm, patch.HeightMm }.Any(n => n is <= 0 or > 100000) ||
            patch.Quantity is <= 0 or > 10000)
            throw new InvalidDataException("Invalid design output.");
        var result = Read(Write(before));
        if (patch.OffTopic) return result;
        foreach (var property in typeof(DesignBrief).GetProperties())
        {
            var field = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            var value = property.GetValue(patch);
            if (patch.ClearFields.Contains(field)) property.SetValue(result, null);
            else if (value is not null) property.SetValue(result, value is string s ? s.Trim() : value);
        }
        return result;
    }

    public static DesignAssessment Assess(DesignBrief b, ManufacturingConfiguration c)
    {
        var issues = new List<string>();
        var alternatives = new List<string>();
        var missing = new List<string>();
        void Need(bool absent, string name) { if (absent) missing.Add(name); }
        Need(string.IsNullOrWhiteSpace(b.Object), "object"); Need(string.IsNullOrWhiteSpace(b.Use), "use");
        Need(b.WidthMm is null || b.DepthMm is null || b.HeightMm is null, "dimensions");
        Need(b.Quantity is null, "quantity"); Need(string.IsNullOrWhiteSpace(b.CategoryId), "categoryId");
        Need(string.IsNullOrWhiteSpace(b.MaterialId), "materialId"); Need(b.Colors is null || b.Colors.Count == 0, "colors");
        Need(b.Personalization is null, "personalization");
        var materials = c.AvailableMaterials().ToList();
        if (!c.Printers.Any(p => p.Active) || materials.Count == 0)
            issues.Add("Por ahora necesitamos revisar contigo las opciones de fabricación disponibles.");
        if (b.CategoryId is not null && !c.Categories.Any(x => x.Active && x.Id == b.CategoryId))
        {
            issues.Add("Este tipo de objeto necesita revisión. Podemos explorar una categoría del catálogo.");
            alternatives.AddRange(c.Categories.Where(x => x.Active).Select(x => $"Categoría: {x.Name}"));
        }
        var material = materials.FirstOrDefault(m => m.Id == b.MaterialId);
        if (b.MaterialId is not null && material is null)
        {
            issues.Add("Ese material no está disponible. Elige una de las opciones del catálogo.");
            alternatives.AddRange(materials.Select(x => $"Material: {x.Name}"));
        }
        if (material is not null && b.Colors?.Any(color => !material.Colors.Any(x => x.Available && string.Equals(x.Name, color, StringComparison.OrdinalIgnoreCase))) == true)
        {
            issues.Add("Alguno de esos colores no está disponible para el material elegido.");
            alternatives.AddRange(material.Colors.Where(x => x.Available).Select(x => $"Usar solo el color: {x.Name}"));
        }
        if (b.WidthMm is not null && b.DepthMm is not null && b.HeightMm is not null)
        {
            var dimensions = new[] { b.WidthMm.Value, b.DepthMm.Value, b.HeightMm.Value }.Order().ToArray();
            var candidates = c.Printers.Where(p => p.Active && (b.MaterialId is null || p.MaterialIds.Contains(b.MaterialId))).ToList();
            bool Fits(PrinterProfile p) {
                var bounds = new[] { p.WidthMm - 2 * p.MarginMm, p.DepthMm - 2 * p.MarginMm, p.HeightMm - p.MarginMm }.Order().ToArray();
                return dimensions.Zip(bounds).All(pair => pair.First <= pair.Second);
            }
            if (candidates.Count > 0 && !candidates.Any(Fits))
            {
                issues.Add("Las medidas exceden lo disponible en una sola pieza. Podemos reducirlas o solicitar revisión para dividir el diseño.");
                var scale = candidates.Max(p => dimensions.Zip(new[] { p.WidthMm - 2 * p.MarginMm, p.DepthMm - 2 * p.MarginMm, p.HeightMm - p.MarginMm }.Order())
                    .Min(pair => pair.Second / pair.First)) * 0.95m;
                var w = decimal.Floor(b.WidthMm.Value * scale); var d = decimal.Floor(b.DepthMm.Value * scale); var h = decimal.Floor(b.HeightMm.Value * scale);
                if (w > 0 && d > 0 && h > 0) alternatives.Add($"Cambiar a ancho {w} mm, fondo {d} mm y alto {h} mm");
            }
            else if (candidates.Count > 0 && !candidates.Any(p => Fits(p) && (b.Colors?.Distinct(StringComparer.OrdinalIgnoreCase).Count() ?? 1) <= p.MaxColors))
            {
                issues.Add("La combinación de tamaño y colores necesita ajuste. Podemos usar menos colores o revisar un ensamblaje.");
                alternatives.AddRange(material?.Colors.Where(x => x.Available).Select(x => $"Usar solo el color: {x.Name}") ?? []);
            }
        }
        else if (b.Colors is { Count: > 0 } && !c.Printers.Any(p => p.Active &&
            (b.MaterialId is null || p.MaterialIds.Contains(b.MaterialId)) && p.MaxColors >= b.Colors.Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            issues.Add("Necesitamos reducir la cantidad de colores o revisar el diseño por piezas.");
        if (issues.Count > 0) return new("NeedsReview", issues[0], alternatives.Distinct().Take(8).ToArray(), issues.ToArray(), missing.ToArray());
        var next = missing.FirstOrDefault();
        var (question, choices) = next switch {
            "object" => ("¿Qué objeto te gustaría crear?", c.Categories.Where(x => x.Active).Select(x => $"Quiero crear: {x.Name}").Take(5).ToArray()),
            "use" => ("¿Para qué lo vas a usar? Cuéntame si estará al aire libre, cerca del calor o soportará peso.", new[] { "Uso decorativo en interior", "Organizar objetos en interior", "Uso funcional: necesito explicar la carga" }),
            "dimensions" => ("¿Cuánto debe medir? Indica ancho, fondo y alto en centímetros o milímetros.", new[] { "Ancho 10 cm, fondo 10 cm, alto 10 cm", "Ancho 15 cm, fondo 10 cm, alto 8 cm" }),
            "quantity" => ("¿Cuántas unidades necesitas?", new[] { "1 unidad", "5 unidades", "10 unidades" }),
            "categoryId" => ("¿En cuál de estas categorías encaja mejor?", c.Categories.Where(x => x.Active).Select(x => $"Categoría: {x.Name}").Take(8).ToArray()),
            "materialId" => ("¿Qué material prefieres? PLA suele usarse en decoración interior; podemos revisar otras necesidades.", materials.Select(x => $"Material: {x.Name}").Take(8).ToArray()),
            "colors" => ("¿Qué color o colores te gustaría usar?", material?.Colors.Where(x => x.Available).Select(x => $"Color: {x.Name}").Take(8).ToArray() ?? []),
            "personalization" => ("¿Quieres añadir un nombre, texto o algún detalle personalizado?", new[] { "Sin personalización", "Quiero añadir un nombre" }),
            _ => ("La ficha está completa para revisión. Puedes corregir cualquier dato. La fabricación y el precio requieren validación posterior.", new[] { "Quiero cambiar las medidas", "Quiero cambiar el color" })
        };
        return new(next is null ? "ReadyForReview" : "Collecting", question, choices, [], missing.ToArray());
    }
}
