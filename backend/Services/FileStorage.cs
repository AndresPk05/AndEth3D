using System.Text;
using System.Text.RegularExpressions;

namespace AndEth3D.Backend.Services;

public class FileStorage(IWebHostEnvironment env)
{
    public const long MaxBytes = 20_000_000;
    private static readonly HashSet<string> AllowedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".gif", ".stl", ".obj", ".3mf", ".pdf", ".zip"];
    private string WebRoot => env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    private string PrivateRoot => Path.Combine(env.ContentRootPath, "App_Data", "attachments");
    public static bool IsImage(string name) => new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }.Contains(Path.GetExtension(name).ToLowerInvariant());

    public async Task<string> SaveAsync(IFormFile file, bool privateFile = false, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || (!privateFile && !IsImage(file.FileName)) || file.Length <= 0 || file.Length > MaxBytes)
            throw new ArgumentException("Invalid upload.");
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > MaxBytes) throw new ArgumentException("Upload too large.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.ToArray();
        if (!ValidContent(extension, bytes)) throw new ArgumentException("Content does not match extension.");
        var directory = privateFile ? PrivateRoot : Path.Combine(WebRoot, "uploads");
        Directory.CreateDirectory(directory);
        var name = $"{Guid.NewGuid():N}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(directory, name), bytes, ct);
        return (privateFile ? "/private/" : "/uploads/") + name;
    }

    public static bool ValidContent(string extension, byte[] bytes)
    {
        var b = bytes.AsSpan();
        bool Starts(byte[] signature) => bytes.AsSpan().StartsWith(signature);
        return extension switch
        {
            ".png" => Starts([137, 80, 78, 71, 13, 10, 26, 10]),
            ".jpg" or ".jpeg" => Starts([255, 216, 255]),
            ".gif" => b.StartsWith("GIF87a"u8) || b.StartsWith("GIF89a"u8),
            ".webp" => b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8),
            ".pdf" => b.StartsWith("%PDF-"u8),
            ".zip" or ".3mf" => Starts([80, 75, 3, 4]),
            ".stl" => (b.Length >= 84 && 84L + 50L * System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b[80..84]) == b.Length)
                || (Encoding.UTF8.GetString(bytes).TrimStart().StartsWith("solid ") && Encoding.UTF8.GetString(bytes).Contains("endsolid")),
            ".obj" => !bytes.Contains((byte)0) && Regex.IsMatch(Encoding.UTF8.GetString(bytes), @"(?m)^v\s+[-+\d.]"),
            _ => false
        };
    }

    public string? ResolvePrivate(string? url)
    {
        if (url is null || !Regex.IsMatch(url, @"^/(private|uploads)/[a-f0-9]{32}\.[a-z0-9]+$")) return null;
        return Path.Combine(url.StartsWith("/private/") ? PrivateRoot : Path.Combine(WebRoot, "uploads"), Path.GetFileName(url));
    }

    public void Delete(string? url)
    {
        var path = ResolvePrivate(url);
        if (path is not null && File.Exists(path)) File.Delete(path);
    }
}
