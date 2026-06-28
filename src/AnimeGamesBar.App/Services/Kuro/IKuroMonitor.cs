using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Kuro;

public interface IKuroMonitor
{
    Task<IReadOnlyList<ArknightsPlayerBinding>> GetBindingsAsync(
        SklandCredential credential,
        CancellationToken cancellationToken);

    Task<WutheringWavesAccountStatus> GetStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken);
}
