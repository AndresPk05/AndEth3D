using System.Text.Json;
using System.Text.RegularExpressions;
using AndEth3D.Backend.Models;

namespace AndEth3D.Backend.Services;

public class TextModelRoute
{
    public string[] Models { get; set; } = [];
    public int MaxOutputTokens { get; set; } = 1200;
    public int TimeoutSeconds { get; set; } = 60;
    public decimal MaxPromptUsdPerMillion { get; set; } = 1;
    public decimal MaxCompletionUsdPerMillion { get; set; } = 5;
}

public class TextModelRouting
{
    public bool Enabled { get; set; }
    public TextModelRoute Simple { get; set; } = new() { Models = ["google/gemini-3.1-flash-lite"], MaxPromptUsdPerMillion = .25m, MaxCompletionUsdPerMillion = 1.5m };
    public TextModelRoute Standard { get; set; } = new() { Models = ["openai/gpt-5.4-mini"], MaxPromptUsdPerMillion = .75m, MaxCompletionUsdPerMillion = 4.5m };
    public TextModelRoute Complex { get; set; } = new() { Models = ["anthropic/claude-sonnet-4.6"], MaxPromptUsdPerMillion = 3, MaxCompletionUsdPerMillion = 15, MaxOutputTokens = 2000, TimeoutSeconds = 90 };
}

public static class ModelRouter
{
    // Classification is advisory, deterministic and free. Manufacturing rules remain authoritative.
    public static string Complexity(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        if (text.Length > 700 || Regex.IsMatch(text, @"\b(tolerancia|encaj\w*|rosca\w*|resist\w*|carga|excepto|contradic\w*|mecanism\w*)\b")) return "complex";
        if (text.Length <= 80 && Regex.IsMatch(text, @"^(\d+([.,]\d+)?(\s*(mm|cm|unidades|piezas))?|pla|petg|blanco|negro|rojo|azul|verde|sin personalizaci[oó]n)[.!]?$")) return "simple";
        return "standard";
    }

    public static ChatOptions Chat(ChatOptions source, string message) => !source.Routing.Enabled ? source : Apply(source,
        Complexity(message) switch { "simple" => source.Routing.Simple, "complex" => source.Routing.Complex, _ => source.Routing.Standard });

    public static ChatOptions Apply(ChatOptions source, TextModelRoute route)
    {
        var copy = (ChatOptions)source.Copy();
        copy.Routing = new(); // Validate the resolved route without recursion.
        copy.Models = route.Models;
        copy.MaxOutputTokens = route.MaxOutputTokens;
        copy.TimeoutSeconds = route.TimeoutSeconds;
        copy.MaxPromptUsdPerMillion = route.MaxPromptUsdPerMillion;
        copy.MaxCompletionUsdPerMillion = route.MaxCompletionUsdPerMillion;
        return copy;
    }

    public static decimal MinimumReservation(ChatOptions source) => !source.Routing.Enabled ? source.ReservationUsd :
        new[] { source.Routing.Simple, source.Routing.Standard, source.Routing.Complex }.Min(r => Apply(source, r).ReservationUsd);

    public static VisualSettings Image(VisualSettings source, DesignBrief brief, bool edit)
    {
        if (!source.Routing.Enabled) return source;
        var route = edit ? source.Routing.Edit : (brief.Colors?.Count > 1 || !string.IsNullOrWhiteSpace(brief.Reference) ||
            Complexity(brief.Personalization ?? "") == "complex") ? source.Routing.Detailed : source.Routing.Draft;
        var copy = JsonSerializer.Deserialize<VisualSettings>(JsonSerializer.Serialize(source, DesignPolicy.Json), DesignPolicy.Json)!;
        copy.Model = route.Model; copy.Resolution = route.Resolution;
        copy.SupportsEditing = route.SupportsEditing; copy.ReservePerAttemptUsd = route.ReservePerAttemptUsd;
        return copy;
    }
}
