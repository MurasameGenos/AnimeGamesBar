using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Arknights;

public interface IArknightsMonitor
{
    Task<ArknightsBindingResult> GetBindingsAsync(
        SklandCredential credential,
        string appCode,
        CancellationToken cancellationToken);

    Task<ArknightsAccountStatus> GetStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken);

    Task<EndfieldAccountStatus> GetEndfieldStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken);
}
