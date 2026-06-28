using System.Text.Json;
using AnimeGamesBar.App.Models;

namespace AnimeGamesBar.App.Services.Arknights;

public sealed class SklandEndfieldStatusMapper
{
    public EndfieldAccountStatus ReadStatus(JsonElement detail, ArknightsPlayerBinding player, DateTimeOffset updatedAt)
    {
        var baseInfo = JsonElementNavigator.Get(detail, "base");
        var playerName = JsonElementNavigator.ReadString(baseInfo, "name", "nickName", "nickname") ??
            JsonElementNavigator.ReadString(detail, "name", "nickName", "nickname") ??
            player.NickName;
        var serverName = JsonElementNavigator.ReadString(baseInfo, "serverName") ??
            player.ServerName ??
            player.ChannelName;

        return new EndfieldAccountStatus(
            playerName,
            serverName,
            ReadSanity(detail),
            ReadDailyActivity(detail),
            ReadWeeklyTasks(detail),
            ReadPassLevel(detail),
            updatedAt);
    }

    private static ResourceMeter ReadSanity(JsonElement detail)
    {
        var dungeon = JsonElementNavigator.Get(detail, "dungeon");
        var current = JsonElementNavigator.ReadInt(dungeon, "curStamina", "current", "value", "count") ??
            JsonElementNavigator.ReadInt(detail, "curStamina", "stamina", "sanity");
        var maximum = JsonElementNavigator.ReadInt(dungeon, "maxStamina", "max", "maximum", "limit") ??
            JsonElementNavigator.ReadInt(detail, "maxStamina", "maxSanity");
        var fullAt = ToDateTime(JsonElementNavigator.ReadLong(dungeon, "maxTs", "completeRecoveryTime", "fullAt"));

        if (current is null && maximum is null)
        {
            return ResourceMeter.Empty;
        }

        return new ResourceMeter(
            Math.Clamp(current ?? 0, 0, Math.Max(maximum ?? current ?? 0, current ?? 0)),
            maximum ?? Math.Max(current ?? 0, 0),
            fullAt);
    }

    private static ProgressStatus ReadDailyActivity(JsonElement detail)
    {
        var daily = JsonElementNavigator.Get(detail, "dailyMission");
        return ReadProgress(
            daily,
            new[] { "dailyActivation", "current", "score", "value" },
            new[] { "maxDailyActivation", "maximum", "total", "max", "limit" });
    }

    private static ProgressStatus ReadWeeklyTasks(JsonElement detail)
    {
        var weekly = JsonElementNavigator.Get(detail, "weeklyMission");
        return ReadProgress(
            weekly,
            new[] { "score", "current", "weeklyScore", "value" },
            new[] { "total", "maximum", "max", "limit" });
    }

    private static ProgressStatus ReadPassLevel(JsonElement detail)
    {
        var pass = JsonElementNavigator.Get(detail, "bpSystem");
        return ReadProgress(
            pass,
            new[] { "curLevel", "level", "current" },
            new[] { "maxLevel", "maximum", "max", "limit" });
    }

    private static ProgressStatus ReadProgress(JsonElement node, string[] currentNames, string[] maximumNames)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return ProgressStatus.Empty;
        }

        var current = JsonElementNavigator.ReadInt(node, currentNames);
        var maximum = JsonElementNavigator.ReadInt(node, maximumNames);
        if (current is null && maximum is null)
        {
            return ProgressStatus.Empty;
        }

        return new ProgressStatus(
            Math.Clamp(current ?? 0, 0, Math.Max(maximum ?? current ?? 0, current ?? 0)),
            maximum ?? Math.Max(current ?? 0, 0));
    }

    private static DateTimeOffset? ToDateTime(long? value)
    {
        if (value is null or <= 0)
        {
            return null;
        }

        var raw = value.Value;
        return raw > 10_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(raw)
            : DateTimeOffset.FromUnixTimeSeconds(raw);
    }
}
