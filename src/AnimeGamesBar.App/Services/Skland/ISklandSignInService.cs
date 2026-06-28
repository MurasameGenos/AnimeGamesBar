using AnimeGamesBar.App.Models;

namespace AnimeGamesBar.App.Services.Skland;

public interface ISklandSignInService
{
    Task<IReadOnlyList<SklandSignInResult>> SignInAsync(
        SklandCredential credential,
        GameDashboardKind game,
        IReadOnlyList<ArknightsPlayerBinding> bindings,
        CancellationToken cancellationToken);
}
