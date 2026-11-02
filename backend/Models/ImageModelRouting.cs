using System.ComponentModel.DataAnnotations;

namespace AndEth3D.Backend.Models;
public class ImageModelRoute
{
    [Required, StringLength(150), RegularExpression(@"^[a-zA-Z0-9_.:-]+/[a-zA-Z0-9_.:-]+$")]
    public string Model { get; set; } = "google/gemini-3.1-flash-image";
    [Required, RegularExpression("^(512|768|1K|1.5K|2K)$")]
    public string Resolution { get; set; } = "1K";
    public bool SupportsEditing { get; set; } = true;
    [Range(typeof(decimal), "0.001", "10", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal ReservePerAttemptUsd { get; set; } = .25m;
}
public class ImageModelRouting
{
    public bool Enabled { get; set; }
    [Required] public ImageModelRoute Draft { get; set; } = new() { Model = "black-forest-labs/flux.2-klein-4b", ReservePerAttemptUsd = .10m };
    [Required] public ImageModelRoute Detailed { get; set; } = new();
    [Required] public ImageModelRoute Edit { get; set; } = new();
}
