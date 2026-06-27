namespace AnimeGamesBar.App.Services;

public interface IClock
{
    DateTimeOffset Now { get; }
}
