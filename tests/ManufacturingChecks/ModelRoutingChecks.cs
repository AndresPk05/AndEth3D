using System.Text.Json;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.Extensions.Options;

static class ModelRoutingChecks
{
    public static async Task Run()
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
        var options = new ChatOptions { Enabled = true, ApiKey = "test", Models = ["legacy/model"] };
        Check(ReferenceEquals(options, ModelRouter.Chat(options, "encaje")), "disabled router preserves legacy settings");
        options.Routing.Enabled = true;
        Check(options.Valid(), "recommended routes validate against session budget");
        var handler = new CaptureHandler();
        var client = new OpenRouterDesignClient(new HttpClient(handler), Options.Create(options));
        foreach (var (message, expected) in new[] { ("20 mm", options.Routing.Simple), ("Una caja azul para organizar", options.Routing.Standard), ("Necesito un encaje con tolerancia", options.Routing.Complex) }) {
            var route = ModelRouter.Chat(options, message);
            await client.CompleteAsync(new(), new {}, [], message, CancellationToken.None);
            using var payload = JsonDocument.Parse(handler.Body!);
            Check(payload.RootElement.GetProperty("models")[0].GetString() == expected.Models[0] &&
                payload.RootElement.GetProperty("max_tokens").GetInt32() == expected.MaxOutputTokens &&
                payload.RootElement.GetProperty("provider").GetProperty("max_price").GetProperty("completion").GetDecimal() == expected.MaxCompletionUsdPerMillion,
                "selected model and bounded output/rates reach provider: " + message);
            Check(route.ReservationUsd <= options.SessionBudgetUsd, "route reservation fits validated budget");
        }
        Check(ModelRouter.Chat(options, "20 mm").ReservationUsd < ModelRouter.Chat(options, "encaje").ReservationUsd, "simple route reserves less than complex route");
        options.Routing.Complex.Models = ["replacement/model"];
        Check(ModelRouter.Chat(options, "encaje").Models[0] == "replacement/model", "model replacement requires configuration only");
        options.Routing.Complex.MaxOutputTokens = 99999;
        Check(!options.Valid(), "invalid expensive route fails configuration validation");
        var images = new VisualSettings { Model = "legacy/image", Routing = new() { Enabled = true } };
        var frozen = JsonSerializer.Serialize(images, DesignPolicy.Json);
        var draft = ModelRouter.Image(images, new(), false);
        var detailed = ModelRouter.Image(images, new() { Colors = ["rojo", "azul"] }, false);
        var edit = ModelRouter.Image(images, new(), true);
        Check(draft.Model == images.Routing.Draft.Model && detailed.Model == images.Routing.Detailed.Model && edit.SupportsEditing,
            "image draft detail and edit routes use task capabilities");
        images.Routing.Draft.Model = "replacement/image";
        var historical = JsonSerializer.Deserialize<VisualSettings>(frozen, DesignPolicy.Json)!;
        Check(ModelRouter.Image(historical, new(), false).Model == draft.Model && images.Model == "legacy/image", "queued image revision remains stable and selection does not mutate configuration");
    }
}
