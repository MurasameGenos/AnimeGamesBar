using System.Text.Json;

namespace AnimeGamesBar.App.Services.Skland;

public interface ISklandClient
{
    Task<JsonDocument> GetJsonAsync(
        SklandCredential credential,
        string pathAndQuery,
        CancellationToken cancellationToken);
}
