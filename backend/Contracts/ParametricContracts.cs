using System.ComponentModel.DataAnnotations;
namespace AndEth3D.Backend.Contracts;
public record ParametricParameters(
    [Required, RegularExpression("^(box|plate|organizer)$")] string Template,
    [Range(0.4,10)] decimal WallMm, [Range(0.4,10)] decimal BaseMm,
    [Range(1,6)] int Columns = 1, [Range(1,6)] int Rows = 1);
public record CreateParametricRequest(Guid RequestId,
    [Required, StringLength(64,MinimumLength=64)] string ExpectedFingerprint,
    [Required] ParametricParameters Parameters, bool AcceptTemplate);
public record ParametricQuoteRequest(Guid RequestId);
public record MeasureParametricRequest(int ExpectedVersion,
    [Range(0.01,100000)] decimal Grams, [Range(1,10000000)] decimal Seconds,
    [Required, StringLength(2000,MinimumLength=3)] string Notes);
