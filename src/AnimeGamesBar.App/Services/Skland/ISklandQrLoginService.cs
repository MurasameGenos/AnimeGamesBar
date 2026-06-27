namespace AnimeGamesBar.App.Services.Skland;

public interface ISklandQrLoginService
{
    Task<SklandQrLoginSession> StartAsync(CancellationToken cancellationToken);

    Task<SklandCredential?> PollAsync(SklandQrLoginSession session, CancellationToken cancellationToken);
}
