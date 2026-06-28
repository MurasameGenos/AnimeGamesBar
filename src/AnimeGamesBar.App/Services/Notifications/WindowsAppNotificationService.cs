using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace AnimeGamesBar.App.Services.Notifications;

public sealed class WindowsAppNotificationService : IAppNotificationService
{
    private static readonly Uri ServerChanBaseUri = new("https://sctapi.ftqq.com");
    private readonly HttpClient _httpClient;
    private AppNotificationOptions _options = new();
    private bool _initialized;

    public WindowsAppNotificationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void Initialize()
    {
        try
        {
            AppNotificationManager.Default.Register();
            _initialized = true;
        }
        catch
        {
            _initialized = false;
        }
    }

    public void Configure(AppNotificationOptions options)
    {
        _options = options;
    }

    public async Task ShowAsync(string title, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_initialized)
        {
            try
            {
                var notification = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message)
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
            }
            catch
            {
            }
        }

        await SendServerChanAsync(title, message, cancellationToken);
    }

    private async Task SendServerChanAsync(
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var sendKey = _options.ServerChanSendKey.Trim();
        if (!_options.ServerChanEnabled || string.IsNullOrWhiteSpace(sendKey))
        {
            return;
        }

        try
        {
            var uri = new Uri(ServerChanBaseUri, $"/{Uri.EscapeDataString(sendKey)}.send");
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["title"] = string.IsNullOrWhiteSpace(title) ? "AnimeGames Bar" : title,
                ["desp"] = string.IsNullOrWhiteSpace(message) ? title : message
            });

            using var response = await _httpClient.PostAsync(uri, content, cancellationToken);
            _ = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
        }
    }
}
