using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Tajiduo;

public interface ITajiduoSignInService
{
    Task<IReadOnlyList<SklandSignInResult>> SignInAsync(
        SklandCredential credential,
        IReadOnlyList<ArknightsPlayerBinding> bindings,
        CancellationToken cancellationToken);
}
