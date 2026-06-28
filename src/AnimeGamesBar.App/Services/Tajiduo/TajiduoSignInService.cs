using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Tajiduo;

public sealed class TajiduoSignInService : ITajiduoSignInService
{
    private readonly TajiduoClient _client;

    public TajiduoSignInService(TajiduoClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<SklandSignInResult>> SignInAsync(
        SklandCredential credential,
        IReadOnlyList<ArknightsPlayerBinding> bindings,
        CancellationToken cancellationToken)
    {
        var results = new List<SklandSignInResult>();
        await SignInAppAsync(credential, results, cancellationToken);

        foreach (var binding in bindings)
        {
            results.Add(await SignInGameAsync(credential, binding, cancellationToken));
        }

        return results;
    }

    private async Task SignInAppAsync(
        SklandCredential credential,
        List<SklandSignInResult> results,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _client.IsAppSignedInAsync(credential, cancellationToken))
            {
                results.Add(new SklandSignInResult("异环", "塔吉多 App", SklandSignInState.AlreadySigned, "今日已签到"));
                return;
            }

            await _client.AppSignInAsync(credential, cancellationToken);
            results.Add(new SklandSignInResult("异环", "塔吉多 App", SklandSignInState.Success, "签到成功"));
        }
        catch (Exception ex)
        {
            var state = IsAlreadySigned(ex.Message) ? SklandSignInState.AlreadySigned : SklandSignInState.Failed;
            results.Add(new SklandSignInResult("异环", "塔吉多 App", state, state == SklandSignInState.AlreadySigned ? "今日已签到" : ex.Message));
        }
    }

    private async Task<SklandSignInResult> SignInGameAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _client.GetGameSignStateAsync(credential, cancellationToken))
            {
                return new SklandSignInResult("异环", binding.NickName, SklandSignInState.AlreadySigned, "今日已签到");
            }

            await _client.GameSignInAsync(credential, binding, cancellationToken);
            return new SklandSignInResult("异环", binding.NickName, SklandSignInState.Success, "签到成功");
        }
        catch (Exception ex)
        {
            var state = IsAlreadySigned(ex.Message) ? SklandSignInState.AlreadySigned : SklandSignInState.Failed;
            return new SklandSignInResult("异环", binding.NickName, state, state == SklandSignInState.AlreadySigned ? "今日已签到" : ex.Message);
        }
    }

    private static bool IsAlreadySigned(string message)
    {
        return message.Contains("已签到", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("已经签到", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("签到过", StringComparison.OrdinalIgnoreCase);
    }
}
