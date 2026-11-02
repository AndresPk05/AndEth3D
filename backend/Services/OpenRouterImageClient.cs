using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AndEth3D.Backend.Models;
using Microsoft.Extensions.Options;

namespace AndEth3D.Backend.Services;

public record ImageCompletion(byte[] Bytes, decimal? CostUsd);
public class ImageProviderException(string message, bool retryable = false, decimal? costUsd = null) : Exception(message)
{
    public bool Retryable { get; } = retryable;
    public decimal? CostUsd { get; } = costUsd;
}
public interface IImageGenerator
{
    Task<ImageCompletion> GenerateAsync(VisualSettings settings, string prompt, byte[]? reference, CancellationToken ct);
}
public class OpenRouterImageClient(HttpClient http, IOptions<ChatOptions> credentials) : IImageGenerator
{
    public async Task<ImageCompletion> GenerateAsync(VisualSettings settings, string prompt, byte[]? reference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credentials.Value.ApiKey)) throw new ImageProviderException("Image credentials missing.");
        if (reference is not null && (!settings.SupportsEditing || !ProposalImageStore.ValidPng(reference))) throw new InvalidDataException("Invalid reference.");
        var body = new Dictionary<string, object> {
            ["model"] = settings.Model, ["prompt"] = prompt, ["n"] = 1,
            ["output_format"] = "png", ["resolution"] = settings.Resolution,
            ["provider"] = new { allow_fallbacks = false }
        };
        if (reference is not null) body["input_references"] = new[] { new { type = "image_url", image_url = new { url = "data:image/png;base64," + Convert.ToBase64String(reference) } } };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/images");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Value.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new ImageProviderException("Image request rejected.", response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable);
        await response.Content.LoadIntoBufferAsync(12_000_000, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        decimal? cost = root.TryGetProperty("usage", out var usage) && usage.TryGetProperty("cost", out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var amount) && amount is >= 0 and <= 99999999 ? amount : null;
        try {
            var data = root.GetProperty("data");
            if (data.GetArrayLength() != 1) throw new InvalidDataException("Expected one image.");
            var bytes = Convert.FromBase64String(data[0].GetProperty("b64_json").GetString()!);
            if (!ProposalImageStore.ValidPng(bytes)) throw new InvalidDataException("Expected a bounded PNG.");
            return new(bytes, cost);
        } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException or InvalidDataException or ArgumentException) {
            throw new ImageProviderException("Invalid image output.", costUsd: cost);
        }
    }
}
