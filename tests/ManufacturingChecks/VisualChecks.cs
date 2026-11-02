using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

class FakeImageGenerator : IImageGenerator
{
    public int Calls { get; private set; }
    public int RejectCount { get; set; }
    public bool Timeout { get; set; }
    public bool ReceivedReference { get; private set; }
    public decimal CostUsd { get; set; } = 0.01m;
    public Task<ImageCompletion> GenerateAsync(VisualSettings settings, string prompt, byte[]? reference, CancellationToken ct)
    {
        Calls++; ReceivedReference = reference is not null;
        if (Timeout) throw new OperationCanceledException();
        if (RejectCount-- > 0) throw new ImageProviderException("temporary", true);
        if (prompt.Contains("SecretBrand") || prompt.Contains("costPerKgCop")) throw new Exception("Internal configuration leaked to image prompt");
        return Task.FromResult(new ImageCompletion(VisualChecks.Png(), CostUsd));
    }
}

static class VisualChecks
{
    public static byte[] Png()
    {
        using var result = new MemoryStream(); result.Write([137,80,78,71,13,10,26,10]);
        void Chunk(string type, byte[] data) {
            var length = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length); result.Write(length);
            var payload = Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); result.Write(payload);
            uint crc = 0xffffffff;
            foreach (var value in payload) { crc ^= value; for (var i = 0; i < 8; i++) crc = (crc & 1) == 1 ? 0xedb88320u ^ (crc >> 1) : crc >> 1; }
            BinaryPrimitives.WriteUInt32BigEndian(length, crc ^ 0xffffffff); result.Write(length);
        }
        Chunk("IHDR", [0,0,0,1,0,0,0,1,8,6,0,0,0]);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true)) zlib.Write([0,255,0,0,255]);
        Chunk("IDAT", compressed.ToArray()); Chunk("IEND", []); return result.ToArray();
    }
    public static async Task Run(HttpClient client, IServiceProvider services, FakeImageGenerator generator)
    {
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
        void Role(string role) { client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", role); }
        async Task<bool> Process() { using var scope = services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<VisualProposalProcessor>().ProcessNextAsync(CancellationToken.None); }
        async Task<JsonElement> List(Guid id) => await client.GetFromJsonAsync<JsonElement>($"/api/chat/conversations/{id}/visuals");
        async Task<HttpResponseMessage> Queue(Guid id, Guid requestId, Guid? parent = null) {
            var state = await List(id);
            return await client.PostAsJsonAsync($"/api/chat/conversations/{id}/visuals", new {requestId, expectedFingerprint = state.GetProperty("fingerprint").GetString(), parentProposalId = parent});
        }
        Guid conversationId;
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var brief = new DesignBrief {Object = "Caja", Use = "Interior", CategoryId = "cajas", WidthMm = 100, DepthMm = 80, HeightMm = 60, Quantity = 1, MaterialId = "pla", Colors = ["Negro"], Personalization = "Sin personalización"};
            var conversation = new ChatConversation {UserId = 1, BriefJson = DesignPolicy.Write(brief)};
            db.ChatConversations.Add(conversation); await db.SaveChangesAsync(); conversationId = conversation.Id;
        }
        Role("Client");
        Check((await client.GetAsync("/api/visual-settings")).StatusCode == HttpStatusCode.Forbidden, "visual settings restricted to admins");
        Role("Admin");
        var configuration = new VisualSettings {Enabled = true, Model = "test/image", SupportsEditing = true, MaxProposalsPerConversation = 10, ConversationBudgetUsd = 10, MaxAttemptsPerUserDay = 30};
        Check((await client.PutAsJsonAsync("/api/visual-settings", new {expectedVersion = 0, configuration})).IsSuccessStatusCode, "admin can configure image model and limits");
        Check((await client.PutAsJsonAsync("/api/visual-settings", new {expectedVersion = 0, configuration})).StatusCode == HttpStatusCode.Conflict, "image settings optimistic concurrency enforced");
        Role("Client");
        var firstId = Guid.NewGuid();
        Check((await Queue(conversationId, firstId)).StatusCode == HttpStatusCode.Accepted, "visual job persisted and accepted asynchronously");
        Check(generator.Calls == 0, "HTTP enqueue does not invoke image provider");
        Check((await Queue(conversationId, firstId)).IsSuccessStatusCode, "duplicate visual request is idempotent");
        Check((await Queue(conversationId, Guid.NewGuid())).StatusCode == HttpStatusCode.Conflict, "one active image job per conversation");
        Check(await Process(), "background processor claims durable job");
        var state = await List(conversationId);
        Check(state.GetProperty("proposals")[0].GetProperty("status").GetString() == "Completed" && generator.Calls == 1, "job generates one completed proposal");
        Check(!state.GetRawText().Contains("test/image") && !state.GetRawText().Contains("reservedUsd") && !state.GetRawText().Contains("SecretBrand"), "visual response excludes model, costs and printer identity");
        var url = state.GetProperty("proposals")[0].GetProperty("imageUrl").GetString()!;
        var downloaded = await client.GetAsync(url);
        Check(downloaded.IsSuccessStatusCode && downloaded.Content.Headers.ContentType?.MediaType == "image/png" && downloaded.Headers.CacheControl?.NoStore == true, "owner reads private PNG with no-store");
        client.DefaultRequestHeaders.Add("X-Test-User", "2");
        Check((await client.GetAsync(url)).StatusCode == HttpStatusCode.NotFound, "other user cannot read image");
        Check((await client.GetAsync($"/api/chat/conversations/{conversationId}/visuals")).StatusCode == HttpStatusCode.NotFound, "other user cannot list proposals");
        client.DefaultRequestHeaders.Remove("X-Test-User");
        string oldFingerprint = state.GetProperty("fingerprint").GetString()!;
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conversation = await db.ChatConversations.FindAsync(conversationId);
            var brief = DesignPolicy.Read(conversation!.BriefJson); brief.WidthMm = 120; conversation.BriefJson = DesignPolicy.Write(brief); conversation.Version++;
            await db.SaveChangesAsync();
        }
        state = await List(conversationId);
        Check(state.GetProperty("proposals")[0].GetProperty("outdated").GetBoolean() && state.GetProperty("proposals")[0].GetProperty("brief").GetProperty("widthMm").GetDecimal() == 100, "old image retains original brief and is marked outdated");
        Check((await client.PostAsJsonAsync($"/api/chat/conversations/{conversationId}/visuals", new {requestId = Guid.NewGuid(), expectedFingerprint = oldFingerprint})).StatusCode == HttpStatusCode.Conflict, "stale browser brief cannot enqueue generation");
        var editedId = Guid.NewGuid();
        Check((await Queue(conversationId, editedId, firstId)).StatusCode == HttpStatusCode.Accepted, "edit references existing owned proposal");
        await Process();
        Check(generator.ReceivedReference && (await List(conversationId)).GetProperty("proposals")[0].GetProperty("brief").GetProperty("widthMm").GetDecimal() == 120, "edit uses private reference and current brief");
        generator.RejectCount = 1;
        await Queue(conversationId, Guid.NewGuid()); await Process();
        state = await List(conversationId);
        Check(state.GetProperty("proposals")[0].GetProperty("status").GetString() == "Queued", "transient rejection schedules bounded retry");
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p = await db.VisualProposals.OrderByDescending(p => p.Number).FirstAsync(); p.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        }
        await Process();
        Check((await List(conversationId)).GetProperty("proposals")[0].GetProperty("status").GetString() == "Completed", "scheduled retry completes without new proposal");
        generator.Timeout = true; await Queue(conversationId, Guid.NewGuid()); await Process(); generator.Timeout = false;
        Check((await List(conversationId)).GetProperty("proposals")[0].GetProperty("status").GetString() == "Failed", "uncertain timeout does not trigger duplicate generation");
        var interruptedId = Guid.NewGuid(); await Queue(conversationId, interruptedId);
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.VisualProposals.FindAsync(interruptedId);
            p!.Status = "Processing"; p.LeaseUntil = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        }
        var calls = generator.Calls; await Process();
        Check(generator.Calls == calls && (await List(conversationId)).GetProperty("proposals")[0].GetProperty("status").GetString() == "Failed", "restart recovers expired lease without repeating uncertain call");
        var staleId = Guid.NewGuid(); await Queue(conversationId, staleId);
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var c = await db.ChatConversations.FindAsync(conversationId);
            var brief = DesignPolicy.Read(c!.BriefJson); brief.WidthMm = 130; c.BriefJson = DesignPolicy.Write(brief); c.Version++; await db.SaveChangesAsync();
        }
        await Process();
        Check(generator.Calls == calls, "queued outdated specification rejected before spending");
        configuration.ConversationBudgetUsd = 0.5m; Role("Admin");
        Check((await client.PutAsJsonAsync("/api/visual-settings", new {expectedVersion = 1, configuration})).IsSuccessStatusCode, "administrator can lower image budget");
        Role("Client");
        Check((await Queue(conversationId, Guid.NewGuid())).StatusCode == HttpStatusCode.TooManyRequests, "image budget enforced before enqueue");
        configuration.ConversationBudgetUsd = 10; configuration.MaxAttempts = 1; configuration.MaxAttemptsPerUserDay = 1; Role("Admin");
        Check((await client.PutAsJsonAsync("/api/visual-settings", new {expectedVersion = 2, configuration})).IsSuccessStatusCode, "daily image quota configurable");
        Role("Client");
        Check((await Queue(conversationId, Guid.NewGuid())).StatusCode == HttpStatusCode.TooManyRequests, "daily image quota enforced independently of conversation budget");
        configuration.Enabled = false; Role("Admin");
        await client.PutAsJsonAsync("/api/visual-settings", new {expectedVersion = 3, configuration}); Role("Client");
        Check((await Queue(conversationId, Guid.NewGuid())).StatusCode == HttpStatusCode.ServiceUnavailable && (await client.GetAsync(url)).IsSuccessStatusCode, "pausing generation preserves access to existing images");
        Check(ProposalImageStore.ValidPng(Png()) && !ProposalImageStore.ValidPng("<svg onload='alert(1)'/>"u8.ToArray()) && !ProposalImageStore.ValidPng(Png()[..^1]), "raster validation rejects active and truncated images");
        var store = services.GetRequiredService<ProposalImageStore>();
        Check(store.Resolve("../../secret.png") is null, "private image path traversal rejected");
        var handler = new ImageHttpHandler(); using var http = new HttpClient(handler);
        var real = new OpenRouterImageClient(http, Options.Create(new ChatOptions {ApiKey = "test-only-key"}));
        var result = await real.GenerateAsync(configuration, "test prompt", Png(), CancellationToken.None);
        Check(result.CostUsd == 0.04m && ProposalImageStore.ValidPng(result.Bytes), "OpenRouter image response decoded and metered");
        using var request = JsonDocument.Parse(handler.Body!);
        Check(request.RootElement.GetProperty("input_references")[0].GetProperty("image_url").GetProperty("url").GetString()!.StartsWith("data:image/png;base64,") && !handler.Body!.Contains("test-only-key"), "reference remains private and credential stays out of body");
        handler.Bad = true;
        try { await real.GenerateAsync(configuration, "test", null, CancellationToken.None); throw new Exception("Invalid provider image accepted"); }
        catch (ImageProviderException ex) { Check(ex.CostUsd == 0.04m, "invalid image preserves reported provider cost"); }
        // Remove only exact private files produced by these test jobs.
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var file in await db.VisualProposals.Where(p => p.ConversationId == conversationId && p.ImageFile != null).Select(p => p.ImageFile!).ToListAsync()) store.Delete(file);
        }
    }
}

class ImageHttpHandler : HttpMessageHandler
{
    public string? Body { get; private set; }
    public bool Bad { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Body = await request.Content!.ReadAsStringAsync(ct);
        var json = JsonSerializer.Serialize(new {data = new[] {new {b64_json = Convert.ToBase64String(Bad ? "<svg/>"u8.ToArray() : VisualChecks.Png())}}, usage = new {cost = 0.04}});
        return new(HttpStatusCode.OK) {Content = new StringContent(json, Encoding.UTF8, "application/json")};
    }
}
