namespace AnimeGamesBar.App.Services.Skland;

public interface ICredentialStore
{
    Task<SklandCredential?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(SklandCredential credential, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
