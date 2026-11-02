using System.ComponentModel.DataAnnotations;

namespace AndEth3D.Backend.Models;

public class VisualSettingsRevision
{
    public int Id { get; set; }
    public int Version { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ConfigurationJson { get; set; } = "{}";
}

public class VisualSettings : IValidatableObject
{
    public bool Enabled { get; set; }
    [StringLength(150)] public string Model { get; set; } = "";
    public bool SupportsEditing { get; set; }
    [Required, RegularExpression("^(512|768|1K|1.5K|2K)$")] public string Resolution { get; set; } = "1K";
    [Range(30, 300)] public int TimeoutSeconds { get; set; } = 120;
    [Range(1, 3)] public int MaxAttempts { get; set; } = 2;
    [Range(1, 20)] public int MaxProposalsPerConversation { get; set; } = 6;
    [Range(1, 100)] public int MaxAttemptsPerUserDay { get; set; } = 12;
    [Range(typeof(decimal), "0.001", "10", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal ReservePerAttemptUsd { get; set; } = 0.25m;
    [Range(typeof(decimal), "0.001", "100", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal ConversationBudgetUsd { get; set; } = 2;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Enabled && (string.IsNullOrWhiteSpace(Model) || !Model.Contains('/')))
            yield return new("Selecciona un identificador de modelo de imágenes válido.");
        if (ConversationBudgetUsd < ReservePerAttemptUsd * MaxAttempts)
            yield return new("El presupuesto debe cubrir todos los intentos reservados de al menos una propuesta.");
        if (MaxAttemptsPerUserDay < MaxAttempts)
            yield return new("El límite diario debe cubrir los intentos de una propuesta.");
    }
}

public class VisualProposal
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public int Number { get; set; }
    public int Version { get; set; }
    public Guid? ParentProposalId { get; set; }
    public int ManufacturingRevisionId { get; set; }
    public int SettingsRevisionId { get; set; }
    public string BriefJson { get; set; } = "{}";
    public string BriefFingerprint { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Status { get; set; } = "Queued";
    public string? FailureCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AttemptCount { get; set; }
    public decimal ReservedUsd { get; set; }
    public string AttemptsJson { get; set; } = "[]";
    public string? ImageFile { get; set; }
}

public class VisualAttempt
{
    public int Number { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Processing";
    public decimal ReservedUsd { get; set; }
    public decimal? ReportedCostUsd { get; set; }
}

public class VisualConversationUsage
{
    public Guid ConversationId { get; set; }
    public int Version { get; set; }
    public int ProposalCount { get; set; }
    public decimal ChargedUsd { get; set; }
    public Guid? ActiveProposalId { get; set; }
}

public class VisualUserUsage
{
    public int UserId { get; set; }
    public int Version { get; set; }
    public DateTime Day { get; set; }
    public int ReservedAttempts { get; set; }
}
