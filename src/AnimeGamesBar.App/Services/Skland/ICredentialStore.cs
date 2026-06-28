namespace AnimeGamesBar.App.Services.Skland;

public interface ICredentialStore
{
    Task<SklandCredential?> LoadAsync(CancellationToken cancellationToken);

    Task<SklandCredential?> LoadAsync(string scope, CancellationToken cancellationToken);

    Task SaveAsync(SklandCredential credential, CancellationToken cancellationToken);

    Task SaveAsync(string scope, SklandCredential credential, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);

    Task ClearAsync(string scope, CancellationToken cancellationToken);
}
