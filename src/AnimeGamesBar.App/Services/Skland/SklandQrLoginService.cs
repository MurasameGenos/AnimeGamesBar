namespace AnimeGamesBar.App.Services.Skland;

public sealed class SklandQrLoginService : ISklandQrLoginService
{
    public Task<SklandQrLoginSession> StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("\u626B\u7801\u767B\u5F55\u63A5\u53E3\u5C1A\u672A\u63A5\u5165\u3002");
    }

    public Task<SklandCredential?> PollAsync(SklandQrLoginSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("\u626B\u7801\u767B\u5F55\u63A5\u53E3\u5C1A\u672A\u63A5\u5165\u3002");
    }
}
