namespace AndEth3D.Backend.Services;

public class FileStorage(IWebHostEnvironment env)
{
    private static readonly HashSet<string> AllowedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".gif", ".stl", ".obj", ".3mf", ".pdf", ".zip"];

    private string WebRoot => env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    public async Task<string> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Tipo de archivo no permitido: {extension}");

        var uploadsDir = Path.Combine(WebRoot, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(uploadsDir, fileName);

        await using var stream = File.Create(path);
        await file.CopyToAsync(stream, ct);

        return $"/uploads/{fileName}";
    }

    public void Delete(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/")) return;
        var path = Path.Combine(WebRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path)) File.Delete(path);
    }
}
