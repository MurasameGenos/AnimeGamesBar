using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Kuro;

public sealed class KuroLoginService : IKuroLoginService
{
    private readonly KuroMobileLoginClient _mobileLoginClient;

    public KuroLoginService(KuroMobileLoginClient mobileLoginClient)
    {
        _mobileLoginClient = mobileLoginClient;
    }

    public async Task<SklandCredential?> LoginAsync(
        Window owner,
        SklandCredential currentCredential,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var window = new KuroMobileLoginWindow(_mobileLoginClient, currentCredential);
        window.Activate();

        return await window.WaitForCredentialAsync(cancellationToken);
    }
}
