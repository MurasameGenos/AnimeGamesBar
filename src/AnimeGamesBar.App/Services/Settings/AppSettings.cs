using AnimeGamesBar.App.Services.Notifications;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Settings;

public sealed record AppSettings(
    bool UseDarkTheme,
    bool AutoSignEnabled,
    bool NotificationsEnabled,
    bool StartWithWindows,
    bool ArknightsAutoRefreshEnabled = false,
    bool EndfieldAutoRefreshEnabled = false,
    bool WutheringWavesAutoRefreshEnabled = false,
    bool YihuanAutoRefreshEnabled = false,
    double ArknightsAutoRefreshIntervalMinutes = 5,
    double EndfieldAutoRefreshIntervalMinutes = 5,
    double WutheringWavesAutoRefreshIntervalMinutes = 5,
    double YihuanAutoRefreshIntervalMinutes = 5,
    bool ManualSignInAllGames = false,
    bool DailyAutoSignEnabled = true,
    bool ServerChanEnabled = false,
    string ServerChanSendKey = "",
    bool NotificationCooldownEnabled = false,
    double NotificationCooldownMinutes = 60,
    string ArknightsGamePath = "",
    string ArknightsScriptPath = "",
    string EndfieldGamePath = "",
    string EndfieldScriptPath = "",
    string WutheringWavesGamePath = "",
    string WutheringWavesScriptPath = "",
    string YihuanGamePath = "",
    string YihuanScriptPath = "",
    IReadOnlyList<NotificationRuleSetting>? NotificationRules = null)
{
    public static AppSettings Default { get; } = new(
        UseDarkTheme: true,
        AutoSignEnabled: true,
        NotificationsEnabled: true,
        StartWithWindows: false);

    public ElementTheme ElementTheme => UseDarkTheme ? ElementTheme.Dark : ElementTheme.Light;
}
