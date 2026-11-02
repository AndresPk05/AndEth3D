using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.EntityFrameworkCore;

// Test doubles only. Production retains BCrypt and signed JWTs.
class TestPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    public bool Verify(string password, string hash) => Hash(password) == hash;
}
class TestTokenIssuer : ITokenIssuer { public string CreateToken(User user) => "test-token-" + user.Id; }

static class GatewayChecks
{
    public static async Task Run(HttpClient client, IServiceProvider services)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        var registration = await client.PostAsJsonAsync("/api/auth/register", new {name = "Example", email = "example@example.invalid", password = "valid-test-password"});
        Check(registration.IsSuccessStatusCode, "auth registration gateway delegates successfully");
        Check((await client.PostAsJsonAsync("/api/auth/register", new {name = "Example", email = "example@example.invalid", password = "valid-test-password"})).StatusCode == HttpStatusCode.Conflict, "duplicate registration still rejected");
        Check((await client.PostAsJsonAsync("/api/auth/login", new {email = "example@example.invalid", password = "wrong-password"})).StatusCode == HttpStatusCode.Unauthorized, "invalid login remains unauthorized");
        Check((await client.PostAsJsonAsync("/api/auth/login", new {email = "example@example.invalid", password = "valid-test-password"})).IsSuccessStatusCode, "login response preserved");
        Check((await client.GetAsync("/api/admin/orders")).StatusCode == HttpStatusCode.Unauthorized, "orders require authentication");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Client");
        Check((await client.GetAsync("/api/admin/orders")).StatusCode == HttpStatusCode.Forbidden, "client cannot access administrator orders");
        Check((await client.GetAsync("/api/auth/me")).IsSuccessStatusCode, "identity adapter resolves current user");
        int quoteId;
        using (var form = new MultipartFormDataContent()) {
            form.Add(new StringContent("New test quote"), "description"); form.Add(new StringContent("No estoy seguro"), "material");
            var response = await client.PostAsync("/api/quotes", form);
            Check(response.StatusCode == HttpStatusCode.Created && response.Headers.Location is not null, "quote creation status and location preserved");
            quoteId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        }
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var converted = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/convert", new {title = "Test order", price = 25000, notes = "test"});
        Check(converted.IsSuccessStatusCode, "quote conversion delegates with valid record validation");
        var orderId = (await converted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();
        Check((await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new {status = "Delivered"})).StatusCode == HttpStatusCode.Conflict, "order transition rules remain in service");
        Check((await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new {status = OrderStatus.InQueue})).IsSuccessStatusCode, "valid order transition preserved");
        using (var form = new MultipartFormDataContent()) {
            form.Add(new StringContent("Photo"), "title"); form.Add(new StringContent("Description"), "description");
            form.Add(new StringContent("Category"), "category"); form.Add(new StringContent("PLA"), "material");
            form.Add(new ByteArrayContent([137,80,78,71,13,10,26,10]), "image", "photo.png");
            var response = await client.PostAsync("/api/gallery", form);
            Check(response.StatusCode == HttpStatusCode.Created, "gallery upload gateway delegates successfully");
            var itemId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
            Check((await client.DeleteAsync($"/api/gallery/{itemId}")).StatusCode == HttpStatusCode.NoContent, "gallery deletion status preserved");
        }
        Check((await client.PostAsync("/api/auth/logout", null)).StatusCode == HttpStatusCode.NoContent, "logout gateway delegates");
        using var scope = services.CreateScope();
        Check((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Id == 1)).SessionVersion == 1, "logout revokes existing sessions in service");
    }
}
