using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndEth3D.Backend.Data;
using Microsoft.EntityFrameworkCore;

static class GuestQuoteChecks
{
    public static async Task Run(HttpClient client, IServiceProvider services)
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
        void Role(string? role) { client.DefaultRequestHeaders.Remove("X-Test-Role"); if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role); }
        async Task<HttpResponseMessage> Send(string email = "test@example.invalid", string material = "No estoy seguro", byte[]? file = null) {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("Visitante"), "contactName");
            form.Add(new StringContent(email), "contactEmail");
            form.Add(new StringContent("Una caja para mi escritorio"), "description");
            form.Add(new StringContent(material), "material");
            if (file is not null) form.Add(new ByteArrayContent(file), "referenceFile", "reference.png");
            return await client.PostAsync("/api/quotes/guest", form);
        }
        Role(null);
        Check((await Send("bad-email")).StatusCode == HttpStatusCode.BadRequest, "guest requires valid contact email");
        Check((await Send(material: "unavailable")).StatusCode == HttpStatusCode.BadRequest, "guest cannot bypass manufacturing catalog");
        Check((await Send(file: new byte[5_000_001])).StatusCode == HttpStatusCode.BadRequest, "guest upload limited before storage");
        var response = await Send(file: VisualChecks.Png());
        Check(response.IsSuccessStatusCode, "anonymous quote submission accepted");
        var body = await response.Content.ReadAsStringAsync();
        Check(!body.Contains("test@example.invalid") && !body.Contains("private/") && !body.Contains("userId"), "guest receives only receipt without private metadata");
        int id;
        string? attachmentPath;
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var quote = await db.QuoteRequests.SingleAsync(q => q.UserId == null && q.ContactEmail == "test@example.invalid");
            id = quote.Id;
            attachmentPath = scope.ServiceProvider.GetRequiredService<AndEth3D.Backend.Services.FileStorage>().ResolvePrivate(quote.ReferenceFileUrl);
            Check(quote.ContactName == "Visitante" && quote.ReferenceFileUrl!.StartsWith("/private/"), "guest contact and private attachment persisted");
            Check(await db.Users.CountAsync(u => u.Email == "test@example.invalid") == 1, "guest does not create or attach to matching account");
        }
        Check((await client.GetAsync($"/api/quotes/{id}/file")).StatusCode == HttpStatusCode.Unauthorized, "guest cannot download by quote id");
        Check((await client.GetAsync("/api/quotes/mine")).StatusCode == HttpStatusCode.Unauthorized, "anonymous history remains private");
        Check((await Send(" TEST@example.invalid ")).IsSuccessStatusCode && (await Send()).IsSuccessStatusCode &&
            (await Send()).StatusCode == HttpStatusCode.TooManyRequests, "normalized email enforces three submissions per day");
        Role("Client");
        Check((await client.GetAsync($"/api/quotes/{id}/file")).StatusCode == HttpStatusCode.NotFound, "matching account email grants no guest attachment access");
        var mine = await client.GetFromJsonAsync<JsonElement>("/api/quotes/mine");
        Check(!mine.EnumerateArray().Any(q => q.GetProperty("id").GetInt32() == id), "guest quote is not assigned to existing account history");
        Role("Admin");
        Check((await client.GetAsync($"/api/quotes/{id}/file")).IsSuccessStatusCode, "administrator can review guest attachment");
        var all = await client.GetFromJsonAsync<JsonElement>("/api/quotes");
        Check(all.EnumerateArray().Any(q => q.GetProperty("id").GetInt32() == id && q.GetProperty("userName").GetString() == "Visitante"), "guest visible with contact in admin quotes");
        var converted = await client.PostAsJsonAsync($"/api/quotes/{id}/convert", new { price = 10000 });
        Check(converted.IsSuccessStatusCode, "admin converts guest quote without creating account");
        var orderId = (await converted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();
        var orders = await client.GetFromJsonAsync<JsonElement>("/api/admin/orders");
        Check(orders.EnumerateArray().Any(o => o.GetProperty("id").GetInt32() == orderId && o.GetProperty("userEmail").GetString() == "test@example.invalid"), "guest order preserves contact for fulfillment");
        if (attachmentPath is not null) File.Delete(attachmentPath);
        Role(null);
        HttpResponseMessage? limited = null;
        for (var i = 0; i < 61; i++) { limited = await Send("invalid"); if (limited.StatusCode == HttpStatusCode.TooManyRequests) break; }
        Check(limited?.StatusCode == HttpStatusCode.TooManyRequests, "public endpoint limits requests before processing");
    }
}
