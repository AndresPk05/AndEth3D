using System.Text;
using AndEth3D.Backend.Data;
using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<FileStorage>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .WithOrigins("http://localhost:4321")
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

app.UseCors("frontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public static class Seed
{
    public static async Task EnsureSeededAsync(AppDbContext db, IConfiguration config)
    {
        if (!await db.Users.AnyAsync(u => u.Role == Roles.Admin))
        {
            db.Users.Add(new User
            {
                Name = config["AdminSeed:Name"] ?? "Administrador",
                Email = (config["AdminSeed:Email"] ?? "admin@andeth3d.com").ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(config["AdminSeed:Password"] ?? "Admin123!"),
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
