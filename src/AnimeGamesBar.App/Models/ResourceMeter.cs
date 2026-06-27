namespace AnimeGamesBar.App.Models;

public sealed record ResourceMeter(
    int Current,
    int Maximum,
    DateTimeOffset? FullAt)
{
    public static ResourceMeter Empty { get; } = new(0, 0, null);
}
