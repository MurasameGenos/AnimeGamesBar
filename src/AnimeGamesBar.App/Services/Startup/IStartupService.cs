namespace AnimeGamesBar.App.Services.Startup;

public interface IStartupService
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}
