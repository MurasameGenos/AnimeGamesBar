using System.Text.Json;
using Windows.Security.Credentials;

namespace AnimeGamesBar.App.Services.Skland;

public sealed class PasswordVaultCredentialStore : ICredentialStore
{
    private const string ResourceName = "AnimeGamesBar.Skland";
    private const string UserName = "default";

    private readonly PasswordVault _vault = new();

    public Task<SklandCredential?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var credential = _vault.FindAllByResource(ResourceName)
                .FirstOrDefault(item => item.UserName == UserName);

            if (credential is null)
            {
                return Task.FromResult<SklandCredential?>(null);
            }

            credential.RetrievePassword();
            return Task.FromResult(JsonSerializer.Deserialize<SklandCredential>(credential.Password));
        }
        catch
        {
            return Task.FromResult<SklandCredential?>(null);
        }
    }

    public Task SaveAsync(SklandCredential credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RemoveExisting();

        var json = JsonSerializer.Serialize(credential with { UpdatedAt = DateTimeOffset.Now });
        _vault.Add(new PasswordCredential(ResourceName, UserName, json));

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RemoveExisting();
        return Task.CompletedTask;
    }

    private void RemoveExisting()
    {
        try
        {
            foreach (var credential in _vault.FindAllByResource(ResourceName).Where(item => item.UserName == UserName))
            {
                _vault.Remove(credential);
            }
        }
        catch
        {
        }
    }
}
