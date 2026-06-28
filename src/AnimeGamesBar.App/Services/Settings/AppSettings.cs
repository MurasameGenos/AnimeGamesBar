using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Settings;

public sealed record AppSettings(
    bool UseDarkTheme,
    bool AutoSignEnabled,
    bool NotificationsEnabled,
    bool StartWithWindows,
    double ArknightsAutoRefreshIntervalMinutes = 5,
    double EndfieldAutoRefreshIntervalMinutes = 5,
    double WutheringWavesAutoRefreshIntervalMinutes = 5,
    bool ManualSignInAllGames = false,
    int AutoSignHour = 9,
    int AutoSignMinute = 0)
{
    public static AppSettings Default { get; } = new(
        UseDarkTheme: true,
        AutoSignEnabled: true,
        NotificationsEnabled: true,
        StartWithWindows: false);

    public ElementTheme ElementTheme => UseDarkTheme ? ElementTheme.Dark : ElementTheme.Light;
}
