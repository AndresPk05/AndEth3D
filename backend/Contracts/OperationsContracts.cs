using System.ComponentModel.DataAnnotations;
namespace AndEth3D.Backend.Contracts;
public record RecordProductionRequest(Guid RequestId, int ManufacturingVersion,
    [Required,StringLength(60)] string PrinterId,
    [Required,StringLength(60)] string CategoryId,
    [Required,StringLength(60)] string MaterialId,
    [Required,RegularExpression("^(simple|standard|complex)$")] string Complexity,
    [Range(1,10000)] int Quantity,
    [Range(0.01,100000000)] decimal Grams,
    [Range(0.01,1000000)] decimal MachineHours,
    [Range(0,1000)] decimal DesignHours,
    [Range(0,1000000)] decimal FinishingHours,
    [Range(0,1000000000)] decimal MaterialCostCop,
    [Range(0,1000000000)] decimal MachineCostCop,
    [Range(0,1000000000)] decimal LaborCostCop,
    [Range(0,1000000000)] decimal OtherCostCop,
    [Required,StringLength(2000,MinimumLength=3)] string Notes);
public record PromoteProductionRequest(int ExpectedCalibrationVersion,int ManufacturingVersion,
    [Required,MinLength(1),MaxLength(100)] List<Guid> RecordIds);
