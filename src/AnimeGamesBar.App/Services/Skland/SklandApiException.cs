namespace AnimeGamesBar.App.Services.Skland;

public sealed class SklandApiException : Exception
{
    public SklandApiException(string message)
        : base(message)
    {
    }

    public SklandApiException(string message, int? statusCode, int? apiCode)
        : base(message)
    {
        StatusCode = statusCode;
        ApiCode = apiCode;
    }

    public int? StatusCode { get; }

    public int? ApiCode { get; }
}
