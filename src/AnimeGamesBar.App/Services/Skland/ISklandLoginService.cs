using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Skland;

public interface ISklandLoginService
{
    Task<SklandCredential?> LoginAsync(Window owner, SklandCredential currentCredential, CancellationToken cancellationToken);
}
