using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AndEth3D.Backend.Models;

static class ManufacturingChecks
{
    public static void Run()
    {
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
        bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);
        var material = new MaterialProfile { Id = "pla", Name = "PLA", Active = true, CostPerKgCop = 80000,
            Colors = [new() { Name = "Negro", Available = true }] };
        var printer = new PrinterProfile { Id = "one", Name = "Principal", Brand = "Internal brand", Model = "Internal model", Active = true, MaterialIds = ["pla"] };
        var config = new ManufacturingConfiguration { Materials = [material], Printers = [printer] };
        Check(Valid(config) && Valid(printer) && Valid(material), "valid manufacturing configuration");
        Check(config.AvailableMaterials().Count() == 1, "compatible stocked material available");
        printer.Active = false;
        Check(!config.AvailableMaterials().Any(), "inactive printer hides materials");
        printer.Active = true; material.Active = false;
        Check(!config.AvailableMaterials().Any(), "disabled material hidden");
        material.Active = true; material.Colors[0].Available = false;
        Check(!config.AvailableMaterials().Any(), "out of stock colors hide material");
        material.Colors[0].Available = true;
        printer.MaterialIds = ["missing"];
        Check(!Valid(config) && !config.AvailableMaterials().Any(), "unknown material reference rejected");
        printer.MaterialIds = ["pla"]; printer.MarginMm = 128;
        Check(!Valid(config), "nonpositive usable volume rejected");
        printer.MarginMm = 5;
        config.Materials.Add(new() { Id = "pla", Name = "Other" });
        Check(!Valid(config), "duplicate identifiers rejected");
        config.Materials.RemoveAt(1);
        material.CostPerKgCop = -1;
        Check(!Valid(material), "negative material cost rejected");
        material.CostPerKgCop = 80000;
        config.Pricing.GrossMargin = 1;
        Check(!Valid(config.Pricing), "100 percent margin rejected");
        config.Pricing.GrossMargin = 0.4m;
        printer.Technology = "SLA";
        Check(!Valid(printer), "unsupported pricing technology rejected");
        printer.Technology = "FDM";
        var historicalJson = JsonSerializer.Serialize(config);
        config.Pricing.MachineHourCop = 9000; material.Name = "Renamed";
        var historical = JsonSerializer.Deserialize<ManufacturingConfiguration>(historicalJson)!;
        Check(historical.Materials[0].Name == "PLA" && historical.Pricing.MachineHourCop == 0,
            "immutable snapshot preserves names and rates");
        config.Printers.Add(new() { Id = "two", Name = "Another", Brand = "Other", Model = "New", Active = true, MaterialIds = ["pla"] });
        printer.Active = false;
        Check(config.AvailableMaterials().Count() == 1 && Valid(config), "additional printer enables same material without code change");
    }
}
