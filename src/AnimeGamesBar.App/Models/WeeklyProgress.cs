namespace AnimeGamesBar.App.Models;

public sealed record WeeklyProgress(int Current, int Maximum)
{
    public static WeeklyProgress Empty(int maximum) => new(0, maximum);
}
