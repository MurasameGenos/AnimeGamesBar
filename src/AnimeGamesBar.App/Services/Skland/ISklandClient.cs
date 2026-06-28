using System.Text.Json;

namespace AnimeGamesBar.App.Services.Skland;

public interface ISklandClient
{
    Task<JsonDocument> GetJsonAsync(
        SklandCredential credential,
        string pathAndQuery,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null);

    Task<JsonDocument> GetJsonAsync(
        SklandCredential credential,
        Uri baseUri,
        string pathAndQuery,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null);

    Task<JsonDocument> PostJsonAsync(
        SklandCredential credential,
        string pathAndQuery,
        object? body,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null);
}
