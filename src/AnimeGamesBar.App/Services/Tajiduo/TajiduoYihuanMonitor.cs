using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Tajiduo;

public sealed class TajiduoYihuanMonitor : ITajiduoMonitor
{
    private readonly TajiduoClient _client;

    public TajiduoYihuanMonitor(TajiduoClient client)
    {
        _client = client;
    }

    public Task<IReadOnlyList<ArknightsPlayerBinding>> GetBindingsAsync(
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        return _client.GetYihuanBindingsAsync(credential, cancellationToken);
    }

    public Task<YihuanAccountStatus> GetStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken)
    {
        return _client.GetYihuanStatusAsync(credential, player, cancellationToken);
    }
}
