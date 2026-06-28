using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Tajiduo;

public interface ITajiduoMonitor
{
    Task<IReadOnlyList<ArknightsPlayerBinding>> GetBindingsAsync(
        SklandCredential credential,
        CancellationToken cancellationToken);

    Task<YihuanAccountStatus> GetStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken);
}
