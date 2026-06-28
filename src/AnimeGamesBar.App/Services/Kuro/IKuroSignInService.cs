using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Kuro;

public interface IKuroSignInService
{
    Task<SklandSignInResult> SignInAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        CancellationToken cancellationToken);
}
