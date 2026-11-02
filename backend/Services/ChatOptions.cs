namespace AndEth3D.Backend.Services;

public class ChatOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string[] Models { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 45;
    public int MaxOutputTokens { get; set; } = 1200;
    public int MaxPromptBytes { get; set; } = 32000;
    public int MaxTurns { get; set; } = 30;
    public int MaxRequestsPerDay { get; set; } = 60;
    public int MaxConversationsPerDay { get; set; } = 5;
    public int MinimumIntervalSeconds { get; set; } = 3;
    public decimal SessionBudgetUsd { get; set; } = 1;
    public decimal MaxPromptUsdPerMillion { get; set; } = 1;
    public decimal MaxCompletionUsdPerMillion { get; set; } = 5;
    // Conservative token ceiling; failed/unknown calls retain their reservation.
    public decimal ReservationUsd => decimal.Ceiling(((MaxPromptBytes + 4096m) * MaxPromptUsdPerMillion +
        MaxOutputTokens * MaxCompletionUsdPerMillion) * Math.Max(1, Models.Length)) / 1_000_000m;
    public bool Valid() => Models is not null && Models.Length <= 3 &&
        (!Enabled || (!string.IsNullOrWhiteSpace(ApiKey) && Models.Length > 0)) &&
        Models.All(m => !string.IsNullOrWhiteSpace(m) && m.Length <= 150) && TimeoutSeconds is >= 5 and <= 90 &&
        MaxOutputTokens is >= 400 and <= 4000 && MaxPromptBytes is >= 4000 and <= 64000 &&
        MaxTurns is >= 1 and <= 60 && MaxRequestsPerDay is >= 1 and <= 500 && MaxConversationsPerDay is >= 1 and <= 20 &&
        MinimumIntervalSeconds is >= 0 and <= 60 && MaxPromptUsdPerMillion is > 0 and <= 100 &&
        MaxCompletionUsdPerMillion is > 0 and <= 100 && SessionBudgetUsd >= ReservationUsd && SessionBudgetUsd <= 100;
}
