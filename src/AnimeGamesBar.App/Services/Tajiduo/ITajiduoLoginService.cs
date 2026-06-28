using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Tajiduo;

public interface ITajiduoLoginService
{
    Task<SklandCredential?> LoginAsync(
        Window owner,
        SklandCredential currentCredential,
        CancellationToken cancellationToken);
}
