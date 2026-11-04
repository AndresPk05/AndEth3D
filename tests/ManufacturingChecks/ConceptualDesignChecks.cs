using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.EntityFrameworkCore;

static class ConceptualDesignChecks
{
    public static async Task Run(HttpClient client, IServiceProvider services)
    {
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
        var plane = new DesignBrief { Object = "Avión estrellado", Reference = "Alas rotas", WidthMm = 200, Colors = ["Rojo"] };
        var car = DesignPolicy.Merge(plane, new DesignPatch { NewIdea = true, Object = "Carro", WidthMm = 100, SuggestedFields = ["widthMm"] });
        Check(car.Object == "Carro" && car.Reference is null && car.Colors is null && car.WidthMm == 100, "replacement idea does not inherit previous object details");
        Check(!VisualPolicy.CanUseReference(car, plane), "car cannot inherit airplane image");
        var smallerPlane = DesignPolicy.Merge(plane, new DesignPatch { WidthMm = 80 });
        Check(smallerPlane.Reference == plane.Reference && VisualPolicy.CanUseReference(smallerPlane, plane), "same-object edits retain their reference");
        var original = new DesignBrief { Object = "Figura", WidthMm = 90 };
        var suggested = DesignPolicy.Merge(original, new DesignPatch { WidthMm = 50, HeightMm = 100, Colors = ["Rojo"], SuggestedFields = ["widthMm", "heightMm", "colors"] });
        Check(suggested.WidthMm == 90 && !suggested.SuggestedFields.Contains("widthMm") && suggested.SuggestedFields.Contains("heightMm"), "suggestions preserve explicit dimensions and track proposed values");
        var confirmed = DesignPolicy.Merge(suggested, new DesignPatch { HeightMm = 100 });
        Check(!confirmed.SuggestedFields.Contains("heightMm") && confirmed.SuggestedFields.Contains("colors"), "explicit confirmation clears only affected suggested field");
        Check(VisualPolicy.CanPreview(new() { Object = "Figura conceptual" }) && !VisualPolicy.CanPreview(new()), "concept preview only requires an object");
        Check(VisualPolicy.Prompt(new() { Object = "Figura", MaterialId = "PLA" }, new()).Contains("Figura"), "concept prompt accepts empty manufacturing catalog");
        Guid conversation;
        int user;
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Earlier metric fixtures include synthetic pending jobs without worker usage records.
            foreach (var fixture in await db.VisualProposals.Where(p => p.Status == "Queued" || p.Status == "Processing").ToListAsync()) fixture.Status = "Failed";
            var visitor = new User { Name = "Concept", Email = $"{Guid.NewGuid()}@example.invalid", PasswordHash = "unused" };
            db.Users.Add(visitor); await db.SaveChangesAsync(); user = visitor.Id;
            var c = new ChatConversation { UserId = user, BriefJson = DesignPolicy.Write(new() { Object = "Figura conceptual", HeightMm = 100, SuggestedFields = ["heightMm"] }) };
            db.ChatConversations.Add(c); conversation = c.Id;
            db.ManufacturingRevisions.Add(new() { Version = await db.ManufacturingRevisions.MaxAsync(r => r.Version) + 1, CreatedByUserId = 1, ConfigurationJson = "{}" });
            db.VisualSettingsRevisions.Add(new() { Version = await db.VisualSettingsRevisions.MaxAsync(r => r.Version) + 1, CreatedByUserId = 1,
                ConfigurationJson = JsonSerializer.Serialize(new VisualSettings { Enabled = true, Model = "test/image", MaxAttempts = 1 }, DesignPolicy.Json) });
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Client");
        client.DefaultRequestHeaders.Add("X-Test-User", user.ToString());
        var url = $"/api/chat/conversations/{conversation}/visuals";
        var state = await client.GetFromJsonAsync<JsonElement>(url);
        Check(state.GetProperty("canGenerate").GetBoolean(), "incomplete concept can preview despite missing technical catalog");
        var fingerprint = state.GetProperty("fingerprint").GetString();
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChatOptions>>().Value;
        var previousLimit = options.DailyGlobalBudgetUsd;
        options.DailyGlobalBudgetUsd = 0.000001m;
        try {
            var denied = await client.PostAsJsonAsync(url, new { requestId = Guid.NewGuid(), expectedFingerprint = fingerprint, autoPreview = true });
            Check(denied.StatusCode == HttpStatusCode.TooManyRequests, "global reservation limit blocks a new visitor before enqueue");
        } finally { options.DailyGlobalBudgetUsd = previousLimit; }
        var first = await client.PostAsJsonAsync(url, new { requestId = Guid.NewGuid(), expectedFingerprint = fingerprint, autoPreview = true });
        Check(first.StatusCode == HttpStatusCode.Accepted, "first preliminary preview reserves and queues normally");
        var second = await client.PostAsJsonAsync(url, new { requestId = Guid.NewGuid(), expectedFingerprint = fingerprint, autoPreview = true });
        Check(second.IsSuccessStatusCode && (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid() ==
            (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), "automatic preview reuses same brief without duplicate spend");
        using (var scope = services.CreateScope()) {
            await scope.ServiceProvider.GetRequiredService<VisualProposalProcessor>().ProcessNextAsync(CancellationToken.None);
        }
        state = await client.GetFromJsonAsync<JsonElement>(url);
        Check(state.GetProperty("proposals")[0].GetProperty("status").GetString() == "Completed", "worker generates concept before manufacturing validation");
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var file = (await db.VisualProposals.SingleAsync(p => p.ConversationId == conversation)).ImageFile;
            if (file is not null && scope.ServiceProvider.GetRequiredService<ProposalImageStore>().Resolve(file) is string path) File.Delete(path);
        }
        client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Remove("X-Test-Role");
    }
}
