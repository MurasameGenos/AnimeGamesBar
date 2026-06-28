using System.Text.Json;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Arknights;

public sealed class SklandArknightsMonitor : IArknightsMonitor
{
    private const int WeeklyAnnihilationMaximum = 1800;
    private const int SecurityServiceDeviceMaximum = 24;
    private const int SecurityServiceStripMaximum = 60;
    private const string ArknightsAppCode = "arknights";

    private readonly ISklandClient _client;

    public SklandArknightsMonitor(ISklandClient client)
    {
        _client = client;
    }

    public async Task<ArknightsBindingResult> GetBindingsAsync(
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(credential, cancellationToken) ?? credential.UserId;

        using var document = await _client.GetJsonAsync(
            credential,
            "/api/v1/game/player/binding",
            cancellationToken);

        SklandApiGuard.ThrowIfError(document.RootElement);

        var bindings = ReadArknightsBindings(document.RootElement, userId);
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

    private static IReadOnlyList<ArknightsPlayerBinding> ReadArknightsBindings(JsonElement root, string? userId)
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
            if (!string.Equals(appCode, ArknightsAppCode, StringComparison.OrdinalIgnoreCase))
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
                TryAddBinding(bindings, player, userId ?? string.Empty, defaultUid);
            }
        }

        return bindings
            .GroupBy(binding => binding.Uid)
            .Select(group => group.First())
            .ToArray();
    }

    private static void TryAddBinding(
        List<ArknightsPlayerBinding> bindings,
        JsonElement player,
        string userId,
        string? defaultUid)
    {
        if (!JsonElementNavigator.TryReadString(player, out var uid, "uid") ||
            !JsonElementNavigator.TryReadString(player, out var nickName, "nickName", "nickname", "name"))
        {
            return;
        }

        var channelName = JsonElementNavigator.ReadString(player, "channelName", "channel") ?? "官服";
        var serverName = JsonElementNavigator.ReadString(player, "serverName", "server", "channelMasterId") ?? string.Empty;
        var binding = new ArknightsPlayerBinding(uid, userId, nickName, channelName, serverName);

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
}
