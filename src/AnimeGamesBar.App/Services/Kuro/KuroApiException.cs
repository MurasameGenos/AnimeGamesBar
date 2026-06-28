namespace AnimeGamesBar.App.Services.Kuro;

public sealed class KuroApiException : Exception
{
    public KuroApiException(string message, int? code = null)
        : base(message)
    {
        Code = code;
    }

    public int? Code { get; }
}
