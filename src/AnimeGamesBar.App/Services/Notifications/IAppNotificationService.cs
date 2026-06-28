namespace AnimeGamesBar.App.Services.Notifications;

public interface IAppNotificationService
{
    void Initialize();

    void Configure(AppNotificationOptions options);

    Task ShowAsync(string title, string message, CancellationToken cancellationToken);
}

public sealed record AppNotificationOptions(
    bool ServerChanEnabled = false,
    string ServerChanSendKey = "");
