namespace AnimeGamesBar.App.Services.Skland;

public sealed class DefaultSklandRequestSigner : ISklandRequestSigner
{
    public void Sign(HttpRequestMessage request, SklandCredential credential, DateTimeOffset timestamp)
    {
        request.Headers.TryAddWithoutValidation("dId", credential.DeviceId);
        request.Headers.TryAddWithoutValidation("platform", "1");
        request.Headers.TryAddWithoutValidation("timestamp", timestamp.ToUnixTimeSeconds().ToString());
    }
}
