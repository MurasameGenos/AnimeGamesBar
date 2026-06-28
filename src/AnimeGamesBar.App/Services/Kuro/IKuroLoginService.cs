using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Kuro;

public interface IKuroLoginService
{
    Task<SklandCredential?> LoginAsync(
        Window owner,
        SklandCredential currentCredential,
        CancellationToken cancellationToken);
}
