using AnimeGamesBar.App;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.Services.Skland;

public sealed class SklandLoginService : ISklandLoginService
{
    public async Task<SklandCredential?> LoginAsync(
        Window owner,
        SklandCredential currentCredential,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var window = new SklandLoginWindow(currentCredential);
        window.Activate();

        var snapshot = await window.WaitForCredentialAsync(cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        return new SklandCredential(
            snapshot.Cred.Trim(),
            snapshot.Token.Trim(),
            snapshot.Cookie.Trim(),
            snapshot.UserId.Trim(),
            string.IsNullOrWhiteSpace(snapshot.DeviceId)
                ? currentCredential.DeviceId
                : snapshot.DeviceId.Trim(),
            DateTimeOffset.Now);
    }
}
