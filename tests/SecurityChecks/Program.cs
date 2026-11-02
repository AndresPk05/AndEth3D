using AndEth3D.Backend.Models;
using AndEth3D.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
Check(OrderStatus.CanTransition(OrderStatus.Pending, OrderStatus.InQueue), "queue transition");
Check(!OrderStatus.CanTransition(OrderStatus.Pending, OrderStatus.Delivered), "cannot skip workflow");
Check(!OrderStatus.CanTransition(OrderStatus.Delivered, OrderStatus.Printing), "terminal state");
Check(OrderStatus.CanTransition(OrderStatus.Printing, OrderStatus.Cancelled), "cancel active order");
Check(!FileStorage.ValidContent(".png", "<script>alert(1)</script>"u8.ToArray()), "reject HTML disguised as image");
Check(FileStorage.ValidContent(".png", [137,80,78,71,13,10,26,10]), "PNG signature");
Check(!FileStorage.ValidContent(".stl", new byte[83]), "reject inconsistent binary STL");
Check(FileStorage.ValidContent(".obj", "v 1 2 3\nf 1 2 3"u8.ToArray()), "OBJ vertex data");
var root = Path.Combine(Path.GetTempPath(), "andeth-security-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try {
 var storage = new FileStorage(new TestEnvironment { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
 Check(storage.ResolvePrivate("/uploads/../../secret") is null, "reject traversal");
 using var stream = new MemoryStream("%PDF-1.7 test"u8.ToArray());
 var file = new FormFile(stream, 0, stream.Length, "file", "test.pdf");
 var url = await storage.SaveAsync(file, privateFile: true);
 Check(url.StartsWith("/private/") && File.Exists(storage.ResolvePrivate(url)), "private attachment outside web root");
 try { await storage.SaveAsync(file); throw new Exception("Public PDF accepted"); } catch (ArgumentException) { Console.WriteLine("PASS gallery rejects non-images"); }
} finally { Directory.Delete(root, true); }
Console.WriteLine("Security checks completed.");
class TestEnvironment : IWebHostEnvironment {
 public string ApplicationName { get; set; } = "Checks";
 public string EnvironmentName { get; set; } = "Development";
 public string ContentRootPath { get; set; } = "";
 public string WebRootPath { get; set; } = "";
 public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
 public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
