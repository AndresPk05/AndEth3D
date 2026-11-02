namespace AndEth3D.Backend.Services;

public enum AppOutcome { Success, Created, Accepted, NoContent, Invalid, NotFound, Unauthorized, Forbidden, Conflict, RateLimited, Unavailable, File }
public record DownloadFile(string Path, string ContentType, string? Name = null, bool Private = true);
public record DownloadBytes(byte[] Bytes, string ContentType, string Name);
public record AppResult(AppOutcome Outcome, object? Body = null, string? ResourceId = null)
{
    public static AppResult Ok(object? body = null) => new(AppOutcome.Success, body);
    public static AppResult Created(string id, object body) => new(AppOutcome.Created, body, id);
    public static AppResult Accepted(object body) => new(AppOutcome.Accepted, body);
    public static AppResult NoContent() => new(AppOutcome.NoContent);
    public static AppResult Invalid(object? body = null) => new(AppOutcome.Invalid, body);
    public static AppResult NotFound() => new(AppOutcome.NotFound);
    public static AppResult Unauthorized(object body) => new(AppOutcome.Unauthorized, body);
    public static AppResult Conflict(object body) => new(AppOutcome.Conflict, body);
    public static AppResult RateLimited(object body) => new(AppOutcome.RateLimited, body);
    public static AppResult Unavailable(object body) => new(AppOutcome.Unavailable, body);
    public static AppResult File(DownloadFile file) => new(AppOutcome.File, file);
    public static AppResult File(DownloadBytes file) => new(AppOutcome.File, file);
}

public interface ICurrentActor
{
    int Id { get; }
    bool IsAdmin { get; }
}
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
public interface ITokenIssuer
{
    string CreateToken(AndEth3D.Backend.Models.User user);
}
