namespace AnimeGamesBar.App.Services.Tajiduo;

public sealed class TajiduoApiException : Exception
{
    public TajiduoApiException(string message, int? code = null)
        : base(message)
    {
        Code = code;
    }

    public int? Code { get; }
}
