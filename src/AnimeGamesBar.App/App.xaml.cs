using AnimeGamesBar.App.Services;
using AnimeGamesBar.App.Services.Arknights;
using AnimeGamesBar.App.Services.Skland;
using AnimeGamesBar.App.ViewModels;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var credentialStore = new PasswordVaultCredentialStore();
        var httpClient = new HttpClient();
        var sklandClient = new SklandClient(httpClient, new SystemClock(), new DefaultSklandRequestSigner());
        var monitor = new SklandArknightsMonitor(sklandClient);
        var qrLogin = new SklandQrLoginService();

        var viewModel = new MainViewModel(credentialStore, monitor, qrLogin);
        _window = new MainWindow(viewModel);
        _window.Activate();
    }
}
