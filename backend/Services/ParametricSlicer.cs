using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace AndEth3D.Backend.Services;

public record SliceResult(decimal Grams,decimal Seconds,string EngineVersion,string EffectiveConfigHash);
public interface IParametricSlicer { Task<SliceResult> Slice(byte[] stl,string ini,SlicingProfile profile,decimal centerX,decimal centerY,CancellationToken ct); }
public static class ParametricProfile
{
    public static bool Safe(string ini) => Encoding.UTF8.GetByteCount(ini)<=1_000_000 && !ini.Contains('\0') &&
        !Regex.IsMatch(ini.TrimStart('\uFEFF'),@"(?im)^[ \t]*(post_process|print_host|printhost_apikey)[ \t]*=[ \t]*[^\s\r\n]");
    public static async Task<string> Read(SlicingProfile profile)
    {
        if(!profile.Valid() || new FileInfo(profile.ProfilePath).Length>1_000_000)throw new InvalidDataException("Invalid slicing profile.");
        var bytes=await File.ReadAllBytesAsync(profile.ProfilePath);
        if(!string.Equals(ParametricGeometry.Hash(bytes),profile.ProfileSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Profile digest mismatch.");
        var ini=new UTF8Encoding(false,true).GetString(bytes);
        if(!Safe(ini))throw new InvalidDataException("Unsupported slicing profile.");
        return ini;
    }
}

// The host only invokes Docker with fixed arguments. STL/INI are server-created,
// read-only inputs; no client file, executable, script or profile path is accepted.
public class DockerParametricSlicer : IParametricSlicer
{
    public async Task<SliceResult> Slice(byte[] stl,string ini,SlicingProfile profile,decimal centerX,decimal centerY,CancellationToken ct)
    {
        if(!profile.Valid() || !ParametricProfile.Safe(ini) || !string.Equals(ParametricGeometry.Hash(Encoding.UTF8.GetBytes(ini)),profile.ProfileSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid profile.");
        var name="andeth-slice-"+Guid.NewGuid().ToString("N");
        var directory=Path.Combine(Path.GetTempPath(),name);Directory.CreateDirectory(directory);
        try{
            await File.WriteAllBytesAsync(Path.Combine(directory,"model.stl"),stl,ct);
            await File.WriteAllTextAsync(Path.Combine(directory,"profile.ini"),ini,new UTF8Encoding(false),ct);
            if(directory.Contains(','))throw new InvalidOperationException("Unsupported temporary path.");
            var args=new[]{"run","--name",name,"--pull=never","--network=none","--read-only","--cpus=1","--memory=768m","--memory-swap=768m","--pids-limit=64",
                "--cap-drop=ALL","--security-opt=no-new-privileges","--user=65532:65532","--log-driver=none",
                "--tmpfs","/tmp:rw,nosuid,nodev,noexec,size=256m,mode=1777","--mount",$"type=bind,source={directory},target=/input,readonly",
                "--env","HOME=/tmp","--env","LC_ALL=C",profile.Image,centerX.ToString(CultureInfo.InvariantCulture),centerY.ToString(CultureInfo.InvariantCulture)};
            var output=await Run(args,ct);
            var result=JsonSerializer.Deserialize<SliceResult>(output,new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException();
            if(result.EngineVersion!=profile.EngineVersion || result.Grams is <=0 or >100000 || result.Seconds is <=0 or >10000000 ||
                !Regex.IsMatch(result.EffectiveConfigHash??"",@"^[a-f0-9]{64}$"))throw new InvalidDataException("Invalid slicing result.");
            return result;
        } finally {
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try{await Run(["rm","-f",name],cleanup.Token);}catch(Exception ex) when(ex is IOException or InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception){}
            // Exact server-owned files only; never recursively delete a computed path.
            foreach(var file in new[]{"model.stl","profile.ini"})File.Delete(Path.Combine(directory,file));
            Directory.Delete(directory);
        }
    }
    private static async Task<string> Run(string[] args,CancellationToken ct)
    {
        var start=new ProcessStartInfo("docker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in args)start.ArgumentList.Add(arg);
        using var process=Process.Start(start) ?? throw new InvalidOperationException("Docker unavailable.");
        async Task<string> Read(StreamReader reader){var text=new StringBuilder();var buffer=new char[1024];int n;while((n=await reader.ReadAsync(buffer.AsMemory(),ct))>0){text.Append(buffer,0,n);if(text.Length>8192)throw new InvalidDataException("Worker output limit.");}return text.ToString();}
        try{
            var stdout=Read(process.StandardOutput);var stderr=Read(process.StandardError);
            // If either pipe hits its bound, terminate immediately instead of waiting
            // for the producer to block on a pipe nobody is reading.
            var exit=process.WaitForExitAsync(ct);
            var tasks=new List<Task>{stdout,stderr,exit};
            while(tasks.Count>0){var completed=await Task.WhenAny(tasks);await completed;tasks.Remove(completed);}
            if(process.ExitCode!=0)throw new InvalidOperationException("Slicing process failed.");
            return await stdout;
        }finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
    }
}
