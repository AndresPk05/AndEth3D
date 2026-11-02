using System.Text.RegularExpressions;
namespace AndEth3D.Backend.Services;
public class ParametricOptions
{
    public bool Enabled { get; set; }
    // Enable this only on a dedicated worker host, not every API replica.
    public bool RunWorker { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxPerConversation { get; set; } = 10;
    public int MaxPerUserDay { get; set; } = 10;
    public List<SlicingProfile> Profiles { get; set; } = [];
    public bool Valid() => TimeoutSeconds is >=30 and <=240 && MaxPerConversation is >=1 and <=50 && MaxPerUserDay is >=1 and <=100 &&
        Profiles.All(p=>p.Valid()) && Profiles.Select(p=>p.Id).Distinct().Count()==Profiles.Count;
}
public class SlicingProfile
{
    public int Priority { get; set; } = 100;
    public string Id { get; set; } = "";
    public string PrinterId { get; set; } = "";
    public string MaterialId { get; set; } = "";
    public string ProfilePath { get; set; } = "";
    public string ProfileSha256 { get; set; } = "";
    public string Image { get; set; } = "";
    public string EngineVersion { get; set; } = "";
    public decimal DesignHours { get; set; }
    public decimal FinishingHours { get; set; }
    public bool TotalsVerified { get; set; }
    public decimal UnreportedPurgeGrams { get; set; }
    public bool Valid() => Regex.IsMatch(Id,@"^[a-z0-9-]{1,60}$") && !string.IsNullOrWhiteSpace(PrinterId) && !string.IsNullOrWhiteSpace(MaterialId) &&
        Path.IsPathFullyQualified(ProfilePath) && Regex.IsMatch(ProfileSha256,@"^[a-fA-F0-9]{64}$") &&
        Regex.IsMatch(Image,@"^[a-zA-Z0-9./:_-]+@sha256:[a-f0-9]{64}$") && EngineVersion.Length is >0 and <=100 &&
        EngineVersion.StartsWith("PrusaSlicer-2.",StringComparison.Ordinal) && TotalsVerified && UnreportedPurgeGrams is >=0 and <=1000 &&
        Priority is >=0 and <=1000 && DesignHours is >=0 and <=1000 && FinishingHours is >=0 and <=1000;
}
