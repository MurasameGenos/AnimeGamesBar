using System.Text.Json;

namespace AnimeGamesBar.App.Services.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly SemaphoreSlim SettingsFileGate = new(1, 1);
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AnimeGamesBar");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return AppSettings.Default;
            }

            await using var stream = File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: cancellationToken) ??
                AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await SettingsFileGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            await using var stream = new FileStream(
                SettingsPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
            await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken);
        }
        finally
        {
            SettingsFileGate.Release();
        }
    }
}
