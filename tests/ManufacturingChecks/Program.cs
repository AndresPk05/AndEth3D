using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using AndEth3D.Backend.Controllers;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;

void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
// Validate the real PostgreSQL migration model without needing a running server.
using (var pg = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=checks").Options))
{
    Check(!pg.Database.HasPendingModelChanges(), "PostgreSQL snapshot matches runtime model");
    var sql = pg.GetService<IMigrator>().GenerateScript("20261102000001_SessionVersion");
    Check(sql.Contains("ManufacturingRevisions") && sql.Contains("ON DELETE RESTRICT"), "PostgreSQL migration script generated");
}
using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseTestServer();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => {});
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(ManufacturingController).Assembly);
await using var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    db.Users.Add(new User { Id = 1, Name = "Test", Email = "test@example.invalid", PasswordHash = "unused" });
    await db.SaveChangesAsync();
}
await app.StartAsync();
using var client = app.GetTestClient();
Check((await client.GetAsync("/api/manufacturing")).StatusCode == HttpStatusCode.Unauthorized, "anonymous cannot read internal configuration");
Check((await client.GetAsync("/api/manufacturing/catalog")).IsSuccessStatusCode, "public catalog available");
client.DefaultRequestHeaders.Add("X-Test-Role", "Client");
Check((await client.GetAsync("/api/manufacturing/history")).StatusCode == HttpStatusCode.Forbidden, "client cannot read history");
client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
var config = new ManufacturingConfiguration {
    Printers = [new() { Id = "a1", Name = "SecretMachine", Brand = "SecretBrand", Model = "SecretModel", Active = true, MaterialIds = ["pla"] }],
    Materials = [new() { Id = "pla", Name = "PLA", Active = true, CostPerKgCop = 80000, Colors = [new() { Name = "Negro", Available = true }] }],
    Categories = [new() { Id = "cajas", Name = "Cajas" }], Pricing = new() { MachineHourCop = 5000, GrossMargin = 0.4m }
};
async Task<HttpResponseMessage> Save(int version) => await client.PutAsJsonAsync("/api/manufacturing", new { expectedVersion = version, configuration = config });
Check((await Save(0)).IsSuccessStatusCode, "admin can publish first revision");
Check((await Save(0)).StatusCode == HttpStatusCode.Conflict, "stale revision rejected");
var catalog = await client.GetStringAsync("/api/manufacturing/catalog");
Check(catalog.Contains("PLA") && !catalog.Contains("Secret") && !catalog.Contains("costPerKg") && !catalog.Contains("pricing"), "catalog excludes machines and internal costs");
config.Pricing.GrossMargin = 1;
Check((await Save(1)).StatusCode == HttpStatusCode.BadRequest, "nested invalid margin rejected by API");
config.Pricing.GrossMargin = 0.4m; config.Printers[0].MaterialIds = ["missing"];
Check((await Save(1)).StatusCode == HttpStatusCode.BadRequest, "invalid material references rejected by API");
config.Printers[0].MaterialIds = ["pla"];
var originalCost = config.Materials[0].CostPerKgCop;
config.Materials[0].CostPerKgCop = -1;
Check((await Save(1)).StatusCode == HttpStatusCode.BadRequest, "nested negative material cost rejected by API");
config.Materials[0].CostPerKgCop = originalCost;
config.Materials[0].Colors[0].Name = " ";
Check((await Save(1)).StatusCode == HttpStatusCode.BadRequest, "nested empty color name rejected by API");
config.Materials[0].Colors[0].Name = "Negro";
var nullResult = await client.PutAsJsonAsync("/api/manufacturing", new {expectedVersion = 1, configuration = new { materials = new object?[] { null }, printers = new object[0], categories = new object[0], pricing = config.Pricing }});
Check(nullResult.StatusCode == HttpStatusCode.BadRequest, "null catalog entries rejected safely");
async Task<HttpResponseMessage> Quote(string material) {
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent("Caja personalizada"), "description"); form.Add(new StringContent(material), "material");
    return await client.PostAsync("/api/quotes", form);
}
client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Client");
Check((await Save(1)).StatusCode == HttpStatusCode.Forbidden, "client cannot modify configuration");
var quote = await Quote("PLA");
Check(quote.StatusCode == HttpStatusCode.Created, "client creates quote for enabled material");
var quoteData = await quote.Content.ReadFromJsonAsync<JsonElement>(); var quoteId = quoteData.GetProperty("id").GetInt32();
Check((await Quote("ABS")).StatusCode == HttpStatusCode.BadRequest, "uncatalogued material rejected");
Check((await client.GetAsync($"/api/manufacturing/quotes/{quoteId}")).StatusCode == HttpStatusCode.Forbidden, "quote internal snapshot restricted to admin");
client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
config.Materials[0].Active = false; config.Pricing.MachineHourCop = 9999;
Check((await Save(1)).IsSuccessStatusCode, "new configuration revision saved");
Check(!(await client.GetStringAsync("/api/manufacturing/catalog")).Contains("PLA"), "disabled material removed from public catalog");
Check((await Quote("PLA")).StatusCode == HttpStatusCode.BadRequest, "disabled material cannot be requested");
Check((await Quote("No estoy seguro")).IsSuccessStatusCode, "advice request remains available");
var frozen = await client.GetFromJsonAsync<JsonElement>($"/api/manufacturing/quotes/{quoteId}");
Check(frozen.GetProperty("version").GetInt32() == 1 && frozen.GetProperty("configuration").GetProperty("pricing").GetProperty("machineHourCop").GetDecimal() == 5000,
    "existing quote retains original rates after change");
Check((await client.GetAsync("/api/manufacturing/history/1")).IsSuccessStatusCode, "historical revision retrievable");
Console.WriteLine("Manufacturing integration checks completed.");

class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].FirstOrDefault();
        if (role is null) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "1"), new(ClaimTypes.Role, role)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
