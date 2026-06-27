using System.Text.Json;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Arknights;

public sealed class SklandArknightsMonitor : IArknightsMonitor
{
    private const int WeeklyAnnihilationMaximum = 1800;
    private const int SecurityServiceMaximum = 84;

    private readonly ISklandClient _client;

    public SklandArknightsMonitor(ISklandClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<ArknightsPlayerBinding>> GetBindingsAsync(
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        using var document = await _client.GetJsonAsync(
            credential,
            "/api/v1/game/player/binding?appCode=arknights",
            cancellationToken);

        SklandApiGuard.ThrowIfError(document.RootElement);

        var bindings = new List<ArknightsPlayerBinding>();
        var data = JsonElementNavigator.Get(document.RootElement, "data");

        foreach (var element in JsonElementNavigator.EnumerateObjects(data))
        {
            TryAddBindingFromObject(bindings, element);
        }

        return bindings
            .GroupBy(binding => binding.Uid)
            .Select(group => group.First())
            .ToArray();
    }

    public async Task<ArknightsAccountStatus> GetStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken)
    {
        var path = $"/api/v1/game/player/info?appCode=arknights&uid={Uri.EscapeDataString(player.Uid)}";
        using var document = await _client.GetJsonAsync(credential, path, cancellationToken);

        SklandApiGuard.ThrowIfError(document.RootElement);

        var data = JsonElementNavigator.Get(document.RootElement, "data");
        var mapper = new SklandArknightsStatusMapper(DateTimeOffset.Now);

        return new ArknightsAccountStatus(
            DoctorName: mapper.ReadDoctorName(data, player.NickName),
            ChannelName: player.ChannelName,
            Sanity: mapper.ReadResource(data, "ap", "sanity", "\u7406\u667A"),
            Drones: mapper.ReadResource(data, "labor", "drone", "\u65E0\u4EBA\u673A"),
            TrainingRoom: mapper.ReadTraining(data),
            Annihilation: mapper.ReadWeeklyProgress(data, WeeklyAnnihilationMaximum, "campaign", "annihilation", "\u527F\u706D"),
            SecurityService: mapper.ReadWeeklyProgress(data, SecurityServiceMaximum, "tower", "sss", "\u4FDD\u5168"),
            UpdatedAt: DateTimeOffset.Now);
    }

    private static void TryAddBindingFromObject(List<ArknightsPlayerBinding> bindings, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (JsonElementNavigator.TryReadString(element, out var uid, "uid", "playerUid", "gameUid") &&
            JsonElementNavigator.TryReadString(element, out var nickName, "nickName", "nickname", "name"))
        {
            bindings.Add(new ArknightsPlayerBinding(
                uid,
                nickName,
                JsonElementNavigator.ReadString(element, "channelName", "channel") ?? "\u5B98\u670D",
                JsonElementNavigator.ReadString(element, "serverName", "server") ?? string.Empty));
        }
    }
}
