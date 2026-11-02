namespace AndEth3D.Backend.Models;

// Append-only actuals. Never replace the customer's accepted estimate or price.
public class ProductionRecord
{
    public Guid Id { get; set; }
    public int OrderId { get; set; }
    public int ManufacturingRevisionId { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string RequestHash { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string MaterialId { get; set; } = "";
    public string PrinterId { get; set; } = "";
    public string Complexity { get; set; } = "standard";
    public int Quantity { get; set; }
    public int? ColorCount { get; set; }
    public decimal? WidthMm { get; set; }
    public decimal? DepthMm { get; set; }
    public decimal? HeightMm { get; set; }
    public decimal Grams { get; set; }
    public decimal MachineHours { get; set; }
    public decimal DesignHours { get; set; }
    public decimal FinishingHours { get; set; }
    public decimal MaterialCostCop { get; set; }
    public decimal MachineCostCop { get; set; }
    public decimal LaborCostCop { get; set; }
    public decimal OtherCostCop { get; set; }
    public decimal? EstimatedTotalCop { get; set; }
    public decimal? ConfirmedTotalCop { get; set; }
    public decimal? EstimatedGrams { get; set; }
    public decimal? EstimatedHours { get; set; }
    public string Notes { get; set; } = "";
}
