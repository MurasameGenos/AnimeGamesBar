using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace AnimeGamesBar.App.Services.Notifications;

public sealed class WindowsAppNotificationService : IAppNotificationService
{
    private bool _initialized;

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

    public Task ShowAsync(string title, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_initialized)
        {
            return Task.CompletedTask;
        }

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

        return Task.CompletedTask;
    }
}
