using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Tajiduo;

public sealed class TajiduoLoginService : ITajiduoLoginService
{
    private readonly TajiduoClient _client;

    public TajiduoLoginService(TajiduoClient client)
    {
        _client = client;
    }

    public async Task<SklandCredential?> LoginAsync(
        Window owner,
        SklandCredential currentCredential,
        CancellationToken cancellationToken)
    {
        var window = new TajiduoLoginWindow(_client, currentCredential);
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var ownerHwnd = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        WinRT.Interop.InitializeWithWindow.Initialize(window, ownerHwnd);
        window.Activate();
        return await window.WaitForCredentialAsync(cancellationToken);
    }
}
