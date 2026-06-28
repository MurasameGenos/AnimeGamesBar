using System.Text.Json;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Arknights;

namespace AnimeGamesBar.App.Services.Skland;

public sealed class SklandSignInService : ISklandSignInService
{
    private const int ArknightsGameId = 1;
    private const int EndfieldGameId = 3;
    private readonly ISklandClient _client;

    public SklandSignInService(ISklandClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<SklandSignInResult>> SignInAsync(
        SklandCredential credential,
        GameDashboardKind game,
        IReadOnlyList<ArknightsPlayerBinding> bindings,
        CancellationToken cancellationToken)
    {
        var results = new List<SklandSignInResult>();
        foreach (var binding in bindings)
        {
            var result = game == GameDashboardKind.Arknights
                ? await SignInArknightsAsync(credential, binding, cancellationToken)
                : await SignInEndfieldAsync(credential, binding, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private async Task<SklandSignInResult> SignInArknightsAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            gameId = ArknightsGameId,
            uid = binding.Uid
        };

        using var document = await _client.PostJsonAsync(
            credential,
            "/api/v1/game/attendance",
            body,
            cancellationToken);

        return ReadArknightsResult(document.RootElement, binding);
    }

    private async Task<SklandSignInResult> SignInEndfieldAsync(
        SklandCredential credential,
        ArknightsPlayerBinding binding,
        CancellationToken cancellationToken)
    {
        var roleId = string.IsNullOrWhiteSpace(binding.RoleId) ? binding.Uid : binding.RoleId;
        var serverId = FirstNotBlank(binding.ChannelMasterId, binding.ServerName, binding.ChannelName) ?? string.Empty;
        var headers = new Dictionary<string, string>
        {
            ["sk-game-role"] = $"{EndfieldGameId}_{roleId}_{serverId}",
            ["referer"] = "https://game.skland.com/",
            ["origin"] = "https://game.skland.com/"
        };

        using var document = await _client.PostJsonAsync(
            credential,
            "/web/v1/game/endfield/attendance",
            null,
            cancellationToken,
            headers);

        return ReadEndfieldResult(document.RootElement, binding);
    }

    private static SklandSignInResult ReadArknightsResult(JsonElement root, ArknightsPlayerBinding binding)
    {
        var code = JsonElementNavigator.ReadInt(root, "code") ?? 0;
        var message = JsonElementNavigator.ReadString(root, "message", "msg") ?? string.Empty;
        if (code != 0)
        {
            return BuildNonSuccessResult("明日方舟", binding, message);
        }

        var awards = new List<string>();
        var awardsElement = JsonElementNavigator.Get(root, "data", "awards");
        if (awardsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var award in awardsElement.EnumerateArray())
            {
                var resource = JsonElementNavigator.Get(award, "resource");
                var name = JsonElementNavigator.ReadString(resource, "name") ?? "奖励";
                var count = JsonElementNavigator.ReadInt(award, "count") ?? 1;
                awards.Add($"{name}x{count}");
            }
        }

        var detail = awards.Count == 0 ? "签到成功" : $"签到成功，获得 {string.Join(", ", awards)}";
        return new SklandSignInResult("明日方舟", binding.NickName, SklandSignInState.Success, detail);
    }

    private static SklandSignInResult ReadEndfieldResult(JsonElement root, ArknightsPlayerBinding binding)
    {
        var code = JsonElementNavigator.ReadInt(root, "code") ?? 0;
        var message = JsonElementNavigator.ReadString(root, "message", "msg") ?? string.Empty;
        if (code != 0)
        {
            return BuildNonSuccessResult("终末地", binding, message);
        }

        var awards = new List<string>();
        var data = JsonElementNavigator.Get(root, "data");
        var awardIds = JsonElementNavigator.Get(data, "awardIds");
        var resourceInfoMap = JsonElementNavigator.Get(data, "resourceInfoMap");
        if (awardIds.ValueKind == JsonValueKind.Array && resourceInfoMap.ValueKind == JsonValueKind.Object)
        {
            foreach (var award in awardIds.EnumerateArray())
            {
                var awardId = JsonElementNavigator.ReadString(award, "id");
                if (string.IsNullOrWhiteSpace(awardId) ||
                    !TryGetPropertyCaseInsensitive(resourceInfoMap, awardId, out var resource))
                {
                    continue;
                }

                var name = JsonElementNavigator.ReadString(resource, "name") ?? "奖励";
                var count = JsonElementNavigator.ReadInt(resource, "count") ?? 1;
                awards.Add($"{name}x{count}");
            }
        }

        var detail = awards.Count == 0 ? "签到成功" : $"签到成功，获得 {string.Join(", ", awards)}";
        return new SklandSignInResult("终末地", binding.NickName, SklandSignInState.Success, detail);
    }

    private static SklandSignInResult BuildNonSuccessResult(
        string gameName,
        ArknightsPlayerBinding binding,
        string message)
    {
        var state = IsAlreadySignedMessage(message)
            ? SklandSignInState.AlreadySigned
            : SklandSignInState.Failed;
        var detail = string.IsNullOrWhiteSpace(message)
            ? (state == SklandSignInState.AlreadySigned ? "今日已签到" : "签到失败")
            : message;

        return new SklandSignInResult(gameName, binding.NickName, state, detail);
    }

    private static bool IsAlreadySignedMessage(string message)
    {
        return message.Contains("已签到", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("已经签到", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("重复", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string? FirstNotBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
