using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Windows.Security.Credentials;

namespace AnimeGamesBar.App.Services.Skland;

public sealed class PasswordVaultCredentialStore : ICredentialStore
{
    private const string ResourceName = "AnimeGamesBar.Skland";
    private const string UserName = "default";
    private const string DefaultScope = "default";
    private static readonly string FallbackDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AnimeGamesBar");

    private readonly PasswordVault _vault = new();

    public Task<SklandCredential?> LoadAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(DefaultScope, cancellationToken);
    }

    public Task<SklandCredential?> LoadAsync(string scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var credential = LoadFromVault(ResourceNameFor(scope));
        if (credential is not null)
        {
            return Task.FromResult<SklandCredential?>(credential);
        }

        return Task.FromResult(LoadFromFallbackFile(scope));
    }

    public Task SaveAsync(SklandCredential credential, CancellationToken cancellationToken)
    {
        return SaveAsync(DefaultScope, credential, cancellationToken);
    }

    public Task SaveAsync(string scope, SklandCredential credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(credential with { UpdatedAt = DateTimeOffset.Now });
        var resourceName = ResourceNameFor(scope);

        try
        {
            RemoveExistingVaultEntries(resourceName);
            _vault.Add(new PasswordCredential(resourceName, UserName, json));
        }
        catch
        {
        }

        SaveFallbackFile(scope, json);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return ClearAsync(DefaultScope, cancellationToken);
    }

    public Task ClearAsync(string scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RemoveExistingVaultEntries(ResourceNameFor(scope));
        RemoveFallbackFile(scope);
        return Task.CompletedTask;
    }

    private SklandCredential? LoadFromVault(string resourceName)
    {
        try
        {
            var credential = _vault.FindAllByResource(resourceName)
                .FirstOrDefault(item => item.UserName == UserName);

            if (credential is null)
            {
                return null;
            }

            credential.RetrievePassword();
            return JsonSerializer.Deserialize<SklandCredential>(credential.Password);
        }
        catch
        {
            return null;
        }
    }

    private static SklandCredential? LoadFromFallbackFile(string scope)
    {
        try
        {
            var fallbackPath = FallbackPathFor(scope);
            if (!File.Exists(fallbackPath))
            {
                return null;
            }

            var protectedBytes = Convert.FromBase64String(File.ReadAllText(fallbackPath));
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<SklandCredential>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveFallbackFile(string scope, string json)
    {
        Directory.CreateDirectory(FallbackDirectory);
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllText(FallbackPathFor(scope), Convert.ToBase64String(protectedBytes));
    }

    private static void RemoveFallbackFile(string scope)
    {
        try
        {
            var fallbackPath = FallbackPathFor(scope);
            if (File.Exists(fallbackPath))
            {
                File.Delete(fallbackPath);
            }
        }
        catch
        {
        }
    }

    private void RemoveExistingVaultEntries(string resourceName)
    {
        try
        {
            foreach (var credential in _vault.FindAllByResource(resourceName).Where(item => item.UserName == UserName))
            {
                _vault.Remove(credential);
            }
        }
        catch
        {
        }
    }

    private static string ResourceNameFor(string scope)
    {
        var safeScope = SafeScope(scope);
        return string.Equals(safeScope, DefaultScope, StringComparison.OrdinalIgnoreCase)
            ? ResourceName
            : $"{ResourceName}.{safeScope}";
    }

    private static string FallbackPathFor(string scope)
    {
        var safeScope = SafeScope(scope);
        return string.Equals(safeScope, DefaultScope, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(FallbackDirectory, "skland.credential")
            : Path.Combine(FallbackDirectory, $"skland.{safeScope}.credential");
    }

    private static string SafeScope(string scope)
    {
        return string.IsNullOrWhiteSpace(scope)
            ? DefaultScope
            : new string(scope.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
