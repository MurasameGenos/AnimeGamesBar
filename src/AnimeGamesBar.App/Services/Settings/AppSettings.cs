using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Settings;

public sealed record AppSettings(
    bool UseDarkTheme,
    bool AutoSignEnabled,
    bool NotificationsEnabled,
    bool StartWithWindows)
{
    public static AppSettings Default { get; } = new(
        UseDarkTheme: true,
        AutoSignEnabled: true,
        NotificationsEnabled: true,
        StartWithWindows: false);

    public ElementTheme ElementTheme => UseDarkTheme ? ElementTheme.Dark : ElementTheme.Light;
}
