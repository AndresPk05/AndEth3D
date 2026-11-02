namespace AndEth3D.Backend.Models;

public class ChatConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public string Title { get; set; } = "Nueva idea";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; }
    public int Attempts { get; set; }
    public decimal ReservedUsd { get; set; }
    public DateTime? BusyUntil { get; set; }
    public string BriefJson { get; set; } = "{}";
}

public class ChatTurn
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public int Sequence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string UserText { get; set; } = "";
    public string AssistantText { get; set; } = "";
    public string Status { get; set; } = "Processing";
    public string? Model { get; set; }
    public decimal ReservedUsd { get; set; }
    public decimal? ReportedCostUsd { get; set; }
    public int? ManufacturingRevisionId { get; set; }
    public string? BriefJson { get; set; }
}

// A shared optimistic lock prevents multiple tabs/instances bypassing daily limits.
public class ChatAllowance
{
    public int UserId { get; set; }
    public int Version { get; set; }
    public DateTime Day { get; set; }
    public int Conversations { get; set; }
    public int Requests { get; set; }
    public DateTime? LastRequestAt { get; set; }
}

public class DesignBrief
{
    public List<string> SuggestedFields { get; set; } = [];
    public string? Object { get; set; }
    public string? Use { get; set; }
    public string? CategoryId { get; set; }
    public decimal? WidthMm { get; set; }
    public decimal? DepthMm { get; set; }
    public decimal? HeightMm { get; set; }
    public int? Quantity { get; set; }
    public string? MaterialId { get; set; }
    public List<string>? Colors { get; set; }
    public string? Personalization { get; set; }
    public string? Reference { get; set; }
}

public class DesignPatch : DesignBrief
{
    public bool OffTopic { get; set; }
    public List<string> ClearFields { get; set; } = [];
}
