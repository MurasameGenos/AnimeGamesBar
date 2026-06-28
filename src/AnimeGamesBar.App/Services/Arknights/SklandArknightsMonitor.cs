using System.Text.Json;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Arknights;

public sealed class SklandArknightsMonitor : IArknightsMonitor
{
    private const int WeeklyAnnihilationMaximum = 1800;
    private const int SecurityServiceDeviceMaximum = 24;
    private const int SecurityServiceStripMaximum = 60;
    private const string EndfieldAppCode = "endfield";

    private readonly ISklandClient _client;

    public SklandArknightsMonitor(ISklandClient client)
    {
        _client = client;
    }

    public async Task<ArknightsBindingResult> GetBindingsAsync(
        SklandCredential credential,
        string appCode,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(credential, cancellationToken) ?? credential.UserId;

        using var document = await _client.GetJsonAsync(
            credential,
            "/api/v1/game/player/binding",
            cancellationToken);

        SklandApiGuard.ThrowIfError(document.RootElement);

        var bindings = ReadGameBindings(document.RootElement, userId, appCode);
        return new ArknightsBindingResult(userId ?? string.Empty, bindings);
    }

    public async Task<ArknightsAccountStatus> GetStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken)
    {
        var path = $"/api/v1/game/player/info?uid={Uri.EscapeDataString(player.Uid)}";
        using var document = await _client.GetJsonAsync(credential, path, cancellationToken);

        SklandApiGuard.ThrowIfError(document.RootElement);

        var data = JsonElementNavigator.Get(document.RootElement, "data");
        var mapper = new SklandArknightsStatusMapper(DateTimeOffset.Now);

        return new ArknightsAccountStatus(
            DoctorName: mapper.ReadDoctorName(data, player.NickName),
            ChannelName: player.ChannelName,
            Sanity: mapper.ReadSanity(data),
            Drones: mapper.ReadDrones(data),
            TrainingRoom: mapper.ReadTraining(data),
            Building: mapper.ReadBuildingStatus(data),
            Annihilation: mapper.ReadCampaign(data, WeeklyAnnihilationMaximum),
            SecurityService: mapper.ReadTower(data, SecurityServiceDeviceMaximum),
            SecurityServiceStrips: mapper.ReadTowerStrips(data, SecurityServiceStripMaximum),
            UpdatedAt: DateTimeOffset.Now);
    }

    public async Task<EndfieldAccountStatus> GetEndfieldStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken)
    {
        var userId = string.IsNullOrWhiteSpace(player.UserId) ? credential.UserId : player.UserId;
        var roleId = FirstNotBlank(player.RoleId, player.Uid) ?? string.Empty;
        var serverId = FirstNotBlank(player.ChannelMasterId, player.ServerName, player.ChannelName) ?? string.Empty;
        var path = "/web/v1/game/endfield/card/detail" +
            $"?roleId={Uri.EscapeDataString(roleId)}" +
            $"&serverId={Uri.EscapeDataString(serverId)}" +
            $"&userId={Uri.EscapeDataString(userId ?? string.Empty)}";

        using var document = await _client.GetJsonAsync(credential, path, cancellationToken);

        SklandApiGuard.ThrowIfError(document.RootElement);

        var detail = JsonElementNavigator.Get(document.RootElement, "data", "detail");
        if (detail.ValueKind == JsonValueKind.Undefined)
        {
            detail = JsonElementNavigator.Get(document.RootElement, "data");
        }

        return new SklandEndfieldStatusMapper().ReadStatus(detail, player, DateTimeOffset.Now);
    }

    private async Task<string?> ResolveUserIdAsync(SklandCredential credential, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await _client.GetJsonAsync(
                credential,
                "/api/v1/user/teenager",
                cancellationToken);

            SklandApiGuard.ThrowIfError(document.RootElement);

            var data = JsonElementNavigator.Get(document.RootElement, "data");
            var teenager = JsonElementNavigator.Get(data, "teenager");

            return FirstNotBlank(
                JsonElementNavigator.ReadString(teenager, "userId"),
                JsonElementNavigator.ReadString(data, "userId"));
        }
        catch (SklandApiException)
        {
            return null;
        }
    }

    private static IReadOnlyList<ArknightsPlayerBinding> ReadGameBindings(JsonElement root, string? userId, string appCodeFilter)
    {
        var data = JsonElementNavigator.Get(root, "data");
        var list = JsonElementNavigator.Get(data, "list");
        var bindings = new List<ArknightsPlayerBinding>();

        if (list.ValueKind != JsonValueKind.Array)
        {
            return bindings;
        }

        foreach (var app in list.EnumerateArray())
        {
            var appCode = JsonElementNavigator.ReadString(app, "appCode");
            if (!string.Equals(appCode, appCodeFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var defaultUid = JsonElementNavigator.ReadString(app, "defaultUid");
            var bindingList = JsonElementNavigator.Get(app, "bindingList");
            if (bindingList.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var player in bindingList.EnumerateArray())
            {
                if (string.Equals(appCodeFilter, EndfieldAppCode, StringComparison.OrdinalIgnoreCase))
                {
                    TryAddEndfieldBindings(bindings, player, userId ?? string.Empty, appCode ?? appCodeFilter);
                    continue;
                }

                TryAddBinding(bindings, player, userId ?? string.Empty, defaultUid, appCode ?? appCodeFilter);
            }
        }

        return bindings
            .GroupBy(binding => string.IsNullOrWhiteSpace(binding.RoleId) ? binding.Uid : binding.RoleId)
            .Select(group => group.First())
            .ToArray();
    }

    private static void TryAddEndfieldBindings(
        List<ArknightsPlayerBinding> bindings,
        JsonElement player,
        string userId,
        string appCode)
    {
        var roles = JsonElementNavigator.Get(player, "roles");
        if (roles.ValueKind != JsonValueKind.Array)
        {
            TryAddBinding(bindings, player, userId, null, appCode);
            return;
        }

        foreach (var role in roles.EnumerateArray())
        {
            var roleId = JsonElementNavigator.ReadString(role, "roleId", "role_id", "uid");
            if (string.IsNullOrWhiteSpace(roleId))
            {
                continue;
            }

            var nickname = JsonElementNavigator.ReadString(role, "nickname", "nickName", "name") ??
                JsonElementNavigator.ReadString(player, "nickName", "nickname", "name") ??
                roleId;
            var serverId = JsonElementNavigator.ReadString(role, "serverId", "channelMasterId") ??
                JsonElementNavigator.ReadString(player, "channelMasterId", "serverId") ??
                string.Empty;
            var serverName = JsonElementNavigator.ReadString(role, "serverName", "server") ?? serverId;
            var channelName = JsonElementNavigator.ReadString(player, "channelName", "channel") ?? serverName;
            var binding = new ArknightsPlayerBinding(
                roleId,
                userId,
                nickname,
                channelName,
                serverName,
                serverId,
                appCode,
                roleId);

            if (IsTruthy(JsonElementNavigator.Get(role, "isDefault")))
            {
                bindings.Insert(0, binding);
            }
            else
            {
                bindings.Add(binding);
            }
        }
    }

    private static void TryAddBinding(
        List<ArknightsPlayerBinding> bindings,
        JsonElement player,
        string userId,
        string? defaultUid,
        string appCode)
    {
        var uid = JsonElementNavigator.ReadString(player, "uid", "roleId", "role_id");
        var nickName = JsonElementNavigator.ReadString(player, "nickName", "nickname", "name");
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(nickName))
        {
            return;
        }

        var channelName = JsonElementNavigator.ReadString(player, "channelName", "channel") ?? "官服";
        var channelMasterId = JsonElementNavigator.ReadString(player, "channelMasterId", "serverId") ?? string.Empty;
        var serverName = JsonElementNavigator.ReadString(player, "serverName", "server") ?? channelMasterId;
        var roleId = JsonElementNavigator.ReadString(player, "roleId", "role_id") ?? string.Empty;
        var binding = new ArknightsPlayerBinding(uid, userId, nickName, channelName, serverName, channelMasterId, appCode, roleId);

        if (uid == defaultUid)
        {
            bindings.Insert(0, binding);
            return;
        }

        bindings.Add(binding);
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
