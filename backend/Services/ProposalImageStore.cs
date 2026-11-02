using System.Buffers.Binary;

namespace AndEth3D.Backend.Services;

public class ProposalImageStore(IWebHostEnvironment environment)
{
    public const int MaxBytes = 8_000_000;
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "proposals");
    public string? Resolve(string? file) => file is not null && file.EndsWith(".png", StringComparison.Ordinal) &&
        Guid.TryParseExact(file[..^4], "N", out _) ? Path.Combine(Root, file) : null;

    // Validate PNG structure, chunk CRCs and dimensions without executing an image decoder.
    public static bool ValidPng(byte[] bytes)
    {
        if (bytes.Length is < 57 or > MaxBytes || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return false;
        var offset = 8; var header = false; var data = false;
        while (offset <= bytes.Length - 12) {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > MaxBytes || offset + 12L + length > bytes.Length) return false;
            var type = bytes.AsSpan(offset + 4, 4);
            uint crc = 0xffffffff;
            foreach (var b in bytes.AsSpan(offset + 4, (int)length + 4)) {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            }
            if ((crc ^ 0xffffffff) != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + (int)length, 4))) return false;
            if (!header) {
                if (!type.SequenceEqual("IHDR"u8) || length != 13) return false;
                var w = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8, 4));
                var h = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 12, 4));
                if (w is 0 or > 4096 || h is 0 or > 4096 || (long)w * h > 16_777_216) return false;
                header = true;
            } else if (type.SequenceEqual("IHDR"u8)) return false;
            if (type.SequenceEqual("IDAT"u8) && length > 0) data = true;
            if (type.SequenceEqual("IEND"u8)) return data && length == 0 && offset + 12 == bytes.Length;
            offset += (int)length + 12;
        }
        return false;
    }

    public async Task<string> SaveAsync(byte[] bytes, CancellationToken ct)
    {
        if (!ValidPng(bytes)) throw new InvalidDataException("Invalid generated PNG.");
        Directory.CreateDirectory(Root);
        var name = Guid.NewGuid().ToString("N") + ".png";
        var path = Resolve(name)!; var temporary = path + ".tmp";
        try { await File.WriteAllBytesAsync(temporary, bytes, ct); File.Move(temporary, path); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return name;
    }
    public async Task<byte[]> ReadAsync(string file, CancellationToken ct)
    {
        var path = Resolve(file) ?? throw new InvalidDataException("Invalid private image path.");
        if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("Image too large.");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        if (!ValidPng(bytes)) throw new InvalidDataException("Invalid reference image.");
        return bytes;
    }
    public void Delete(string file) { var path = Resolve(file); if (path is not null && File.Exists(path)) File.Delete(path); }
}
