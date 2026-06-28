using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Kuro;

public sealed class KuroLoginService : IKuroLoginService
{
    public async Task<SklandCredential?> LoginAsync(
        Window owner,
        SklandCredential currentCredential,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var window = new KuroLoginWindow(currentCredential);
        window.Activate();

        var snapshot = await window.WaitForCredentialAsync(cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        return new SklandCredential(
            currentCredential.Cred,
            snapshot.Token.Trim(),
            snapshot.Cookie.Trim(),
            snapshot.UserId.Trim(),
            string.IsNullOrWhiteSpace(snapshot.DeviceId)
                ? currentCredential.DeviceId
                : snapshot.DeviceId.Trim(),
            DateTimeOffset.Now);
    }
}
