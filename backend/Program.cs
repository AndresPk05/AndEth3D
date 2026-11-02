using System.Text;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using AndEth3D.Backend.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32 || jwtKey.StartsWith("dev-only"))
    throw new InvalidOperationException("Configure Jwt:Key with a random secret of at least 32 bytes.");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default")))
    throw new InvalidOperationException("Configure ConnectionStrings:Default.");
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
if (origins.Any(o => !Uri.TryCreate(o, UriKind.Absolute, out var uri) ||
    (uri.Scheme != "https" && !(builder.Environment.IsDevelopment() && uri.Scheme == "http"))))
    throw new InvalidOperationException("Cors:Origins must contain explicit HTTPS origins (HTTP is allowed in development).");
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<ITokenIssuer>(s => s.GetRequiredService<TokenService>());
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
builder.Services.AddApplicationServices();
builder.Services.AddOptions<GuestDesignOptions>().Bind(builder.Configuration.GetSection("GuestDesign"))
    .Validate(o => o.MaxSessionsPerDay is >= 1 and <= 100, "GuestDesign daily limit must be 1..100.").ValidateOnStart();
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddOptions<ChatOptions>().Bind(builder.Configuration.GetSection("OpenRouter"))
    .Validate(o => o.Valid(), "Configure OpenRouter credentials, models and positive usage limits.").ValidateOnStart();
builder.Services.AddHttpClient<IDesignAgent, OpenRouterDesignClient>(http => http.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<ProposalImageStore>();
builder.Services.AddScoped<VisualProposalProcessor>();
builder.Services.AddHttpClient<IImageGenerator, OpenRouterImageClient>(http => http.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHostedService<VisualProposalWorker>();
builder.Services.AddOptions<ParametricOptions>().Bind(builder.Configuration.GetSection("Parametric"))
    .Validate(o=>o.Valid(),"Configure bounded parametric limits and pinned, verified slicing profiles.").ValidateOnStart();
builder.Services.AddSingleton<IParametricSlicer,DockerParametricSlicer>();
builder.Services.AddScoped<ParametricProcessor>();
builder.Services.AddHostedService<ParametricWorker>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var version = context.Principal?.FindFirst("session_version")?.Value;
                if (!int.TryParse(id, out var userId) || !int.TryParse(version, out var tokenVersion) ||
                    !await db.Users.AnyAsync(u => u.Id == userId && u.SessionVersion == tokenVersion))
                    context.Fail("Session revoked.");
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddGuestQuoteRateLimits();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(origins)
        .WithExposedHeaders("Content-Disposition")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await Seed.EnsureSeededAsync(db, app.Configuration);
}

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    // Legacy quote attachments must never remain publicly downloadable.
    if (context.Request.Path.StartsWithSegments("/uploads", out var remainder))
    {
        var name = remainder.Value?.TrimStart('/');
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (string.IsNullOrEmpty(name) || name.Contains('/') || name.Contains('\\') ||
            await db.QuoteRequests.AnyAsync(q => q.ReferenceFileUrl == "/uploads/" + name) ||
            !await db.GalleryItems.AnyAsync(g => g.ImageUrl == "/uploads/" + name) ||
            !FileStorage.IsImage(name))
        { context.Response.StatusCode = 404; return; }
    }
    try { await next(context); }
    catch (DbUpdateConcurrencyException)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { message = "El registro cambio. Recargue e intente nuevamente." });
    }
    catch (ArgumentException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { message = "Archivo o datos no validos." });
    }
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseCors("frontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseGuestDesignAccess();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

public static class Seed
{
    public static async Task EnsureSeededAsync(AppDbContext db, IConfiguration config)
    {
        if (!await db.Users.AnyAsync(u => u.Role == Roles.Admin))
        {
            var adminPassword = config["AdminSeed:Password"];
            if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 12 || Encoding.UTF8.GetByteCount(adminPassword) > 72)
                throw new InvalidOperationException("Set AdminSeed:Password (12 characters minimum, 72 UTF-8 bytes maximum) for initial setup.");
            db.Users.Add(new User
            {
                Name = config["AdminSeed:Name"] ?? "Administrador",
                Email = (config["AdminSeed:Email"] ?? "admin@andeth3d.com").ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = Roles.Admin,
            });
        }

        if (!await db.GalleryItems.AnyAsync())
        {
            db.GalleryItems.AddRange(
                new GalleryItem
                {
                    Title = "Figura decorativa de zorro",
                    Description = "Figura impresa en PLA con acabado sedoso, ideal para escritorio o estantería.",
                    Category = "Figuras",
                    Material = "PLA Silk",
                },
                new GalleryItem
                {
                    Title = "Soporte para celular",
                    Description = "Soporte resistente con ángulo ajustable, impreso en PETG para mayor durabilidad.",
                    Category = "Accesorios",
                    Material = "PETG",
                },
                new GalleryItem
                {
                    Title = "Maceta geométrica",
                    Description = "Maceta con diseño facetado y drenaje, disponible en varios colores.",
                    Category = "Hogar",
                    Material = "PLA",
                },
                new GalleryItem
                {
                    Title = "Llavero personalizado",
                    Description = "Llaveros con nombre o logo, perfectos para regalos y emprendimientos.",
                    Category = "Personalizados",
                    Material = "PLA",
                });
        }

        await db.SaveChangesAsync();
    }
}
