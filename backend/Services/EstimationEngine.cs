using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Services;

public static class EstimationEngine
{
    public const string Method = "observed-envelope-v1";
    public static DesignEstimate Calculate(DesignBrief b, ManufacturingConfiguration config, int tariff,
        int calibration, IReadOnlyList<CalibrationSample> samples, string complexity)
    {
        DesignEstimate Review(string message) => new("ReviewRequired", message, Method, tariff, calibration, complexity, b.Quantity ?? 0);
        if (DesignPolicy.Assess(b, config).Status != "ReadyForReview") return Review("Completa la ficha y resuelve las incompatibilidades antes de estimar.");
        var dimensions = new[] {b.WidthMm!.Value,b.DepthMm!.Value,b.HeightMm!.Value}.Order().ToArray();
        var candidates = samples.Where(s => s.CategoryId == b.CategoryId && s.MaterialId == b.MaterialId && s.Complexity == complexity && s.ColorCount == b.Colors!.Count)
            .GroupBy(s => s.PrinterId).OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in candidates) {
            var printer = config.Printers.SingleOrDefault(p => p.Id == group.Key && p.Active && p.MaterialIds.Contains(b.MaterialId!) && p.MaxColors >= b.Colors!.Count);
            if (printer is null) continue;
            var usable = new[] {printer.WidthMm-2*printer.MarginMm,printer.DepthMm-2*printer.MarginMm,printer.HeightMm-printer.MarginMm}.Order().ToArray();
            if (dimensions.Where((d,i) => d > usable[i]).Any()) continue;
            var rows = group.ToArray();
            if (rows.Length < 3 || rows.Select(s => s.Evidence.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 3) continue;
            var sizes = rows.Select(s => new[]{s.WidthMm,s.DepthMm,s.HeightMm}.Order().ToArray()).ToArray();
            if (dimensions.Where((d,i) => d < sizes.Min(s=>s[i]) || d > sizes.Max(s=>s[i])).Any()) continue;
            var volume = dimensions.Aggregate(1m, (v,d)=>v*d);
            decimal Volume(CalibrationSample s) => s.WidthMm*s.DepthMm*s.HeightMm;
            // Observed envelope plus 25% uncertainty; never claims slicing accuracy.
            var grams = new EstimateRange(rows.Min(s=>volume*s.Grams/Volume(s))*.75m, rows.Max(s=>volume*s.Grams/Volume(s))*1.25m);
            var hours = new EstimateRange(rows.Min(s=>volume*s.MachineHours/Volume(s))*.75m, rows.Max(s=>volume*s.MachineHours/Volume(s))*1.25m);
            var material = config.Materials.Single(m => m.Id == b.MaterialId);
            var rates = config.Pricing;
            decimal Price(decimal g, decimal h, decimal design, decimal finish) => Math.Max(rates.MinimumOrderCop,
                ((g*material.CostPerKgCop/1000+h*rates.MachineHourCop+finish*rates.FinishingHourCop)*b.Quantity!.Value+design*rates.DesignHourCop)/(1-rates.GrossMargin));
            var low = Price(grams.Min,hours.Min,rows.Min(s=>s.DesignHours)*.75m,rows.Min(s=>s.FinishingHours)*.75m);
            var high = Price(grams.Max,hours.Max,rows.Max(s=>s.DesignHours)*1.25m,rows.Max(s=>s.FinishingHours)*1.25m);
            EstimateRange Round(EstimateRange r) => new(decimal.Floor(r.Min*100)/100,decimal.Ceiling(r.Max*100)/100);
            var total = Round(new(low,high));
            return new("Indicative", "Precio orientativo; requiere revisión técnica y confirmación administrativa.", Method, tariff, calibration, complexity, b.Quantity!.Value,
                Round(grams),Round(hours),Round(new(total.Min/b.Quantity.Value,total.Max/b.Quantity.Value)),total,decimal.Round((total.Min+total.Max)/2,2),
                ["Consumo por pieza, incluidos soportes, purga y desperdicio observados.","Rango observado ampliado un 25%; no es un intervalo estadístico ni un laminado.",
                 "Diseño cobrado una vez; fabricación y acabado por unidad. Sin descuentos por lote.","Incluye margen y mínimo de pedido. No incluye envío; impuestos adicionales deben confirmarse en revisión.","La complejidad seleccionada y la resistencia se validarán al revisar el diseño."]);
        }
        return Review("Esta combinación de categoría, material, colores, complejidad o medidas necesita calibración y revisión manual.");
    }
}
