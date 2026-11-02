using System.ComponentModel.DataAnnotations;
using AndEth3D.Backend.Models;
namespace AndEth3D.Backend.Contracts;
public record SaveCalibrationRequest([Range(0,int.MaxValue)] int ExpectedVersion, int ManufacturingVersion,
    [Required, MaxLength(1000)] List<CalibrationSample> Samples);
public record SubmitDesignQuoteRequest(Guid RequestId, Guid ProposalId,
    [Required, StringLength(64, MinimumLength=64)] string EstimateToken,
    [Required, RegularExpression("^(simple|standard|complex)$")] string Complexity);
public record ReviewDesignQuoteRequest(int ExpectedVersion,
    [Range(typeof(decimal), "0.01", "9999999999.99", ParseLimitsInInvariantCulture=true, ConvertValueInInvariantCulture=true)] decimal PriceCop,
    [Required, StringLength(5000, MinimumLength=3)] string Notes);
public record EstimateRange(decimal Min, decimal Max);
public record DesignEstimate(string Status, string Message, string Method, int TariffVersion, int CalibrationVersion,
    string Complexity, int Quantity, EstimateRange? GramsPerUnit = null, EstimateRange? MachineHoursPerUnit = null,
    EstimateRange? UnitPriceCop = null, EstimateRange? TotalPriceCop = null, decimal? AverageTotalCop = null,
    string[]? Assumptions = null);
