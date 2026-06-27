namespace AnimeGamesBar.App.Services.Skland;

public interface ISklandRequestSigner
{
    void Sign(HttpRequestMessage request, SklandCredential credential, DateTimeOffset timestamp);
}
