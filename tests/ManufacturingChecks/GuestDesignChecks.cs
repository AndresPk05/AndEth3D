using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndEth3D.Backend.Services;
using Microsoft.Extensions.Options;

static class GuestDesignChecks
{
    public static async Task Run(HttpClient client, IServiceProvider services)
    {
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        var settings = services.GetRequiredService<IOptions<GuestDesignOptions>>().Value;
        settings.Enabled = false;
        Check((await client.PostAsync("/api/design-guest", null)).StatusCode == HttpStatusCode.ServiceUnavailable, "guest design can be disabled");
        settings.Enabled = true;
        var result = await client.PostAsync("/api/design-guest", null);
        Check(result.IsSuccessStatusCode, "design guest starts without registration");
        var session = await result.Content.ReadFromJsonAsync<JsonElement>();
        var user = session.GetProperty("user");
        Check(user.GetProperty("role").GetString() == "Guest" && user.GetProperty("email").GetString() == "", "guest token has distinct role and no invented public contact");
        settings.MaxSessionsPerDay = 1;
        Check((await client.PostAsync("/api/design-guest", null)).StatusCode == HttpStatusCode.TooManyRequests, "guest global daily cap bounds identity cycling");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Guest");
        client.DefaultRequestHeaders.Add("X-Test-User", user.GetProperty("id").GetInt32().ToString());
        var created = await client.PostAsync("/api/chat/conversations", null);
        Check(created.IsSuccessStatusCode, "guest creates private design conversation");
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Check((await client.GetAsync($"/api/chat/conversations/{id}")).IsSuccessStatusCode, "guest can resume owned design");
        Check((await client.GetAsync($"/api/chat/conversations/{Guid.NewGuid()}")).StatusCode == HttpStatusCode.NotFound, "guest cannot access foreign conversations");
        Check((await client.GetAsync("/api/auth/me")).StatusCode == HttpStatusCode.Forbidden, "guest identity cannot access account APIs");
        Check((await client.PostAsJsonAsync($"/api/estimation/conversations/{id}/quotes", new {})).StatusCode == HttpStatusCode.Forbidden, "guest quote requires contact form instead of anonymous internal user");
        foreach (var route in new[] { $"/api/estimation/conversations/{id}/quotes/", $"/api/parametric/models/{id}/quotes/" })
            Check((await client.PostAsJsonAsync(route, new {})).StatusCode == HttpStatusCode.Forbidden, "guest cannot bypass role policy with a trailing slash");
        Check((await client.GetAsync("/api/admin/orders")).StatusCode == HttpStatusCode.Forbidden, "guest cannot access administration");
        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        Check((await client.GetAsync($"/api/chat/conversations/{id}")).StatusCode == HttpStatusCode.Unauthorized, "design still requires private session token");
    }
}
