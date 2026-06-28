using AnimeGamesBar.App.Services;
using AnimeGamesBar.App.Services.Arknights;
using AnimeGamesBar.App.Services.Kuro;
using AnimeGamesBar.App.Services.Notifications;
using AnimeGamesBar.App.Services.Settings;
using AnimeGamesBar.App.Services.Skland;
using AnimeGamesBar.App.Services.Startup;
using AnimeGamesBar.App.ViewModels;
using Microsoft.UI.Xaml;
using System.Net;

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
        var httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });
        var sklandClient = new SklandClient(httpClient, new SystemClock(), new DefaultSklandRequestSigner());
        var monitor = new SklandArknightsMonitor(sklandClient);
        var login = new SklandLoginService();
        var signIn = new SklandSignInService(sklandClient);
        var kuroClient = new KuroClient(httpClient);
        var kuroMonitor = new KuroWutheringWavesMonitor(kuroClient);
        var kuroSignIn = new KuroSignInService(kuroClient);
        var kuroLogin = new KuroLoginService();
        var settingsStore = new JsonSettingsStore();
        var notificationService = new WindowsAppNotificationService();
        var startupService = new RegistryStartupService();
        notificationService.Initialize();

        var viewModel = new MainViewModel(
            credentialStore,
            monitor,
            login,
            signIn,
            kuroMonitor,
            kuroSignIn,
            kuroLogin,
            settingsStore,
            notificationService,
            startupService);
        _window = new MainWindow(viewModel);
        _window.Activate();
    }
}
