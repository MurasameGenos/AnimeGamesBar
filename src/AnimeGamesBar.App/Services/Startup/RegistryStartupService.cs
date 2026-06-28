using Microsoft.Win32;

namespace AnimeGamesBar.App.Services.Startup;

public sealed class RegistryStartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AnimeGamesBar";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return !string.IsNullOrWhiteSpace(key?.GetValue(ValueName) as string);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ??
            Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
