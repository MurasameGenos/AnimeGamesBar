namespace AnimeGamesBar.App.Services.Notifications;

public interface IAppNotificationService
{
    void Initialize();

    Task ShowAsync(string title, string message, CancellationToken cancellationToken);
}
