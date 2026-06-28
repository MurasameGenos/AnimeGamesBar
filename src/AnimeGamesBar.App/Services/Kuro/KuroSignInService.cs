using System.Text.Json;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Kuro;

public sealed class KuroSignInService : IKuroSignInService
{
    private const int GameId = 3;
    private const string WutheringWavesServerId = "76402e5b20be2c39f095a152090afddc";
    private readonly KuroClient _client;

    public KuroSignInService(KuroClient client)
    {
        _client = client;
    }

    public async Task<SklandSignInResult> SignInAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        CancellationToken cancellationToken)
    {
        var month = DateTime.Now.ToString("yyyyMM");
        try
        {
            using var document = await _client.PostFormAsync(
                credential,
                "/encourage/signIn/v2",
                new Dictionary<string, string>
                {
                    ["gameId"] = GameId.ToString(),
                    ["serverId"] = WutheringWavesServerId,
                    ["roleId"] = binding.Uid,
                    ["userId"] = binding.UserId,
                    ["reqMonth"] = month
                },
                cancellationToken,
                browserLike: true);

            var reward = await GetRewardAsync(credential, binding, cancellationToken);
            var message = string.IsNullOrWhiteSpace(reward)
                ? "签到成功"
                : $"签到成功，奖励 {reward}";
            return new SklandSignInResult("鸣潮", binding.NickName, SklandSignInState.Success, message);
        }
        catch (KuroApiException ex) when (ex.Code == 1511)
        {
            var reward = await GetRewardAsync(credential, binding, cancellationToken);
            var message = string.IsNullOrWhiteSpace(reward)
                ? "今日已签到"
                : $"今日已签到，奖励 {reward}";
            return new SklandSignInResult("鸣潮", binding.NickName, SklandSignInState.AlreadySigned, message);
        }
        catch (Exception ex)
        {
            return new SklandSignInResult("鸣潮", binding.NickName, SklandSignInState.Failed, ex.Message);
        }
    }

    private async Task<string> GetRewardAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await _client.PostFormAsync(
                credential,
                "/encourage/signIn/queryRecordV2",
                new Dictionary<string, string>
                {
                    ["gameId"] = GameId.ToString(),
                    ["serverId"] = WutheringWavesServerId,
                    ["roleId"] = binding.Uid,
                    ["userId"] = binding.UserId
                },
                cancellationToken,
                browserLike: true);

            var data = KuroClient.Get(document.RootElement, "data");
            if (data.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            var first = data.EnumerateArray().FirstOrDefault();
            return KuroClient.ReadString(first, "goodsName", "name") ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
