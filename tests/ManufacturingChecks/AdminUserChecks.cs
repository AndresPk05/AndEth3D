using System.Net;
using System.Net.Http.Json;
using AndEth3D.Backend.Contracts;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using Microsoft.EntityFrameworkCore;

internal static class AdminUserChecks
{
    public static async Task Run(HttpClient testClient, IServiceProvider services)
    {
        using var client = testClient;
        const string path = "/api/admin/users";
        var request = new CreateUserRequest(" Cuenta de prueba ", "CUENTA@example.invalid", "Test-only-password", Roles.Admin);

        Check((await client.GetAsync(path)).StatusCode == HttpStatusCode.Unauthorized, "anonymous cannot list users");
        client.DefaultRequestHeaders.Add("X-Test-Role", Roles.Client);
        Check((await client.GetAsync(path)).StatusCode == HttpStatusCode.Forbidden, "client cannot list users");
        Check((await client.PostAsJsonAsync(path, request)).StatusCode == HttpStatusCode.Forbidden, "client cannot create users");
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Add("X-Test-Role", Roles.Admin);

        Check((await client.PostAsJsonAsync(path, request with { Role = "Guest" })).StatusCode == HttpStatusCode.BadRequest, "admin cannot create guest accounts");
        Check((await client.PostAsJsonAsync(path, request with { Password = new string('é', 37) })).StatusCode == HttpStatusCode.BadRequest, "passwords are limited to 72 UTF-8 bytes");
        using var response = await client.PostAsJsonAsync(path, request);
        var created = await response.Content.ReadFromJsonAsync<UserDto>();
        Check(response.StatusCode == HttpStatusCode.Created && created is { Email: "cuenta@example.invalid", Name: "Cuenta de prueba", Role: Roles.Admin }, "admin creates account with normalized details");
        Check(!(await response.Content.ReadAsStringAsync()).Contains("password", StringComparison.OrdinalIgnoreCase), "creation response excludes credentials");
        Check((await client.PostAsJsonAsync(path, request with { Email = request.Email.ToLowerInvariant() })).StatusCode == HttpStatusCode.Conflict, "duplicate email rejected after normalization");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.Users.SingleAsync(u => u.Id == created!.Id);
        Check(persisted.PasswordHash != request.Password, "password is stored as a hash");
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, request.Password));
        Check(login.IsSuccessStatusCode, "created administrator can log in");

        db.Users.Add(new User { Name = "Visitor", Email = "admin-check@guest.invalid", Role = "Guest" });
        db.Users.AddRange(Enumerable.Range(1, 26).Select(i => new User { Name = $"Client {i}", Email = $"admin-check-{i}@example.invalid" }));
        await db.SaveChangesAsync();
        var first = (await client.GetFromJsonAsync<UsersPage>(path))!;
        Check(first.Users.Count == 25 && first.NextAfterId == first.Users[^1].Id, "user list returns a bounded first page");
        var second = (await client.GetFromJsonAsync<UsersPage>($"{path}?afterId={first.NextAfterId}"))!;
        var users = first.Users.Concat(second.Users).ToArray();
        Check(second.NextAfterId is null && second.Users.Count > 0 && users.Select(u => u.Id).Distinct().Count() == users.Length
            && users.All(u => u.Role != "Guest") && users.Contains(created!), "pagination includes managed accounts exactly once and excludes guest sessions");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine($"PASS {name}");
    }
}
