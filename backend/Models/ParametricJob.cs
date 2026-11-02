namespace AndEth3D.Backend.Models;

public class ParametricJob
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public int ManufacturingRevisionId { get; set; }
    public int Version { get; set; }
    public string BriefJson { get; set; } = "{}";
    public string Fingerprint { get; set; } = "";
    public string ParametersJson { get; set; } = "{}";
    public string TemplateVersion { get; set; } = "orthogonal-v1";
    public byte[] ModelStl { get; set; } = [];
    public string ModelHash { get; set; } = "";
    public string? ProfileJson { get; set; }
    public string? ProfileIni { get; set; }
    public string Status { get; set; } = "ModelReady";
    public string? Failure { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? Grams { get; set; }
    public decimal? Seconds { get; set; }
    public string? EstimateJson { get; set; }
    public string? ReproductionHash { get; set; }
    public decimal? ActualGrams { get; set; }
    public decimal? ActualSeconds { get; set; }
    public string? MeasurementNotes { get; set; }
    public int? MeasuredBy { get; set; }
    public DateTime? MeasuredAt { get; set; }
}
public class ParametricAllowance
{
    public int UserId { get; set; }
    public int Version { get; set; }
    public DateTime Day { get; set; }
    public int Count { get; set; }
}
