namespace AnimeGamesBar.App.Services.Skland;

public sealed class SklandApiException : Exception
{
    public SklandApiException(string message)
        : base(message)
    {
    }
}
