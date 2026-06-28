using System.Text.Json;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Kuro;

public sealed class KuroSignInService : IKuroSignInService
{
    private const int GameId = 3;
    private const string DefaultWutheringWavesServerId = "76402e5b20be2c39f095a152090afddc";
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
        var month = DateTime.Now.ToString("MM");
        var serverId = ResolveServerId(binding);
        var userId = ResolveUserId(credential, binding);
        try
        {
            var init = await InitSignInAsync(credential, binding, serverId, userId, cancellationToken);
            var alreadySigned = IsTruthy(KuroClient.Get(init, "isSigIn"));
            if (alreadySigned)
            {
                var alreadyReward = await GetRewardAsync(credential, binding, serverId, userId, cancellationToken);
                var alreadyMessage = string.IsNullOrWhiteSpace(alreadyReward)
                    ? BuildSignInProgress(init, "今日已签到")
                    : $"{BuildSignInProgress(init, "今日已签到")}，奖励 {alreadyReward}";
                return new SklandSignInResult("鸣潮", binding.NickName, SklandSignInState.AlreadySigned, alreadyMessage);
            }

            using var document = await _client.PostFormAsync(
                credential,
                "/encourage/signIn/v2",
                new Dictionary<string, string>
                {
                    ["gameId"] = GameId.ToString(),
                    ["serverId"] = serverId,
                    ["roleId"] = binding.Uid,
                    ["userId"] = userId,
                    ["reqMonth"] = month
                },
                cancellationToken,
                browserLike: true);

            var reward = await GetRewardAsync(credential, binding, serverId, userId, cancellationToken);
            var message = string.IsNullOrWhiteSpace(reward)
                ? BuildSignInProgress(init, "签到成功")
                : $"{BuildSignInProgress(init, "签到成功")}，奖励 {reward}";
            return new SklandSignInResult("鸣潮", binding.NickName, SklandSignInState.Success, message);
        }
        catch (KuroApiException ex) when (ex.Code == 1511)
        {
            var reward = await GetRewardAsync(credential, binding, serverId, userId, cancellationToken);
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

    private async Task<JsonElement> InitSignInAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        string serverId,
        string userId,
        CancellationToken cancellationToken)
    {
        using var document = await _client.PostFormAsync(
            credential,
            "/encourage/signIn/initSignInV2",
            new Dictionary<string, string>
            {
                ["gameId"] = GameId.ToString(),
                ["serverId"] = serverId,
                ["roleId"] = binding.Uid,
                ["userId"] = userId
            },
            cancellationToken,
            browserLike: true);

        return KuroClient.Get(document.RootElement, "data").Clone();
    }

    private async Task<string> GetRewardAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        string serverId,
        string userId,
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
                    ["serverId"] = serverId,
                    ["roleId"] = binding.Uid,
                    ["userId"] = userId
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

    private static string ResolveServerId(ArknightsPlayerBinding binding)
    {
        return string.IsNullOrWhiteSpace(binding.ChannelMasterId)
            ? DefaultWutheringWavesServerId
            : binding.ChannelMasterId;
    }

    private static string ResolveUserId(SklandCredential credential, ArknightsPlayerBinding binding)
    {
        return string.IsNullOrWhiteSpace(binding.UserId)
            ? credential.UserId
            : binding.UserId;
    }

    private static string BuildSignInProgress(JsonElement initData, string prefix)
    {
        var signedDays = KuroClient.ReadInt(initData, "sigInNum") ?? 0;
        var missedDays = KuroClient.ReadInt(initData, "omissionNnm", "omissionNum") ?? 0;
        var nextSignedDays = prefix.Contains("成功", StringComparison.Ordinal) ? signedDays + 1 : signedDays;
        var progress = nextSignedDays > 0 ? $"，本月已签 {nextSignedDays} 天" : string.Empty;
        var missed = missedDays > 0 ? $"，漏签 {missedDays} 天" : string.Empty;
        return $"{prefix}{progress}{missed}";
    }

    private static bool IsTruthy(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var boolean)
                ? boolean
                : string.Equals(value.GetString(), "1", StringComparison.Ordinal),
            _ => false
        };
    }
}
