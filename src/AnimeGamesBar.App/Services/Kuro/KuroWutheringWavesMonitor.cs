using System.Text.Json;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Skland;

namespace AnimeGamesBar.App.Services.Kuro;

public sealed class KuroWutheringWavesMonitor : IKuroMonitor
{
    private const int GameId = 3;
    private readonly KuroClient _client;

    public KuroWutheringWavesMonitor(KuroClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<ArknightsPlayerBinding>> GetBindingsAsync(
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        using var document = await _client.PostFormAsync(
            credential,
            "/user/role/findRoleList",
            new Dictionary<string, string> { ["gameId"] = GameId.ToString() },
            cancellationToken);

        var data = KuroClient.Get(document.RootElement, "data");
        if (data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ArknightsPlayerBinding>();
        }

        var bindings = new List<ArknightsPlayerBinding>();
        foreach (var role in data.EnumerateArray())
        {
            var roleId = KuroClient.ReadString(role, "roleId");
            var roleName = KuroClient.ReadString(role, "roleName") ?? roleId;
            if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(roleName))
            {
                continue;
            }

            var serverId = KuroClient.ReadString(role, "serverId") ?? string.Empty;
            var serverName = KuroClient.ReadString(role, "serverName") ?? serverId;
            var userId = KuroClient.ReadString(role, "userId") ?? credential.UserId;
            var binding = new ArknightsPlayerBinding(
                roleId,
                userId,
                roleName,
                serverName,
                serverName,
                serverId,
                "wutheringwaves",
                roleId);

            if (IsTruthy(KuroClient.Get(role, "isDefault")))
            {
                bindings.Insert(0, binding);
            }
            else
            {
                bindings.Add(binding);
            }
        }

        return bindings;
    }

    public async Task<WutheringWavesAccountStatus> GetStatusAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        CancellationToken cancellationToken)
    {
        using var document = await _client.PostFormAsync(
            credential,
            "/gamer/widget/game3/refresh",
            new Dictionary<string, string>
            {
                ["gameId"] = GameId.ToString(),
                ["roleId"] = player.Uid,
                ["serverId"] = player.ChannelMasterId,
                ["type"] = "2",
                ["sizeType"] = "1"
            },
            cancellationToken,
            browserLike: true);

        var data = KuroClient.Get(document.RootElement, "data");
        var weeklyVoyage = ReadWeeklyVoyage(data);
        if (weeklyVoyage.Maximum <= 0)
        {
            weeklyVoyage = await ReadWeeklyVoyageFromBaseDataAsync(credential, player, weeklyVoyage, cancellationToken);
        }

        var status = new WutheringWavesAccountStatus(
            PlayerName: KuroClient.ReadString(data, "roleName") ?? player.NickName,
            ServerName: KuroClient.ReadString(data, "serverName") ?? player.ServerName,
            Waveplates: ReadResource(data, "energyData", "结晶波片"),
            CrystalSolvent: ReadResource(data, "storeEnergyData", "结晶单质"),
            DailyActivity: ReadResource(data, "livenessData", "每日活跃度"),
            WeeklyVoyage: weeklyVoyage,
            WeeklyBoss: ReadResource(data, "weeklyData", "战歌重奏次数"),
            BattlePassLevel: ReadBattlePassLevel(data),
            TowerResetAt: ReadTimestamp(data, "towerData", "refreshTimeStamp"),
            SeaResetAt: ReadTimestamp(data, "slashTowerData", "refreshTimeStamp"),
            FinalBattleEndAt: ReadFinalBattleTime(data),
            HasSignedIn: IsTruthy(KuroClient.Get(data, "hasSignIn")),
            UpdatedAt: DateTimeOffset.Now);

        return status;
    }

    private static WutheringWavesResourceStatus ReadResource(JsonElement root, string propertyName, string fallbackName)
    {
        var value = KuroClient.Get(root, propertyName);
        return new WutheringWavesResourceStatus(
            KuroClient.ReadString(value, "name") ?? fallbackName,
            KuroClient.ReadInt(value, "cur") ?? 0,
            KuroClient.ReadInt(value, "total") ?? 0,
            ReadTimestamp(value, "refreshTimeStamp"),
            ReadTimestamp(value, "expireTimeStamp"),
            KuroClient.ReadString(value, "value") ?? string.Empty);
    }

    private static WutheringWavesResourceStatus ReadBattlePassLevel(JsonElement root)
    {
        var entry = TryReadBattlePassEntry(
            root,
            "先约电台等级",
            name => name.Contains("等级", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("电台", StringComparison.OrdinalIgnoreCase));

        return entry is null
            ? new WutheringWavesResourceStatus("先约电台等级", 0, 70)
            : entry with { Maximum = 70 };
    }

    private static WutheringWavesResourceStatus ReadWeeklyVoyage(JsonElement root)
    {
        var weeklyFrame = KuroClient.Get(root, "weeklyFrameData");
        if (weeklyFrame.ValueKind == JsonValueKind.Object)
        {
            return ReadResource(root, "weeklyFrameData", "周度游历");
        }

        var weeklyRouge = KuroClient.Get(root, "weeklyRougeData");
        if (weeklyRouge.ValueKind == JsonValueKind.Object)
        {
            return ReadResource(root, "weeklyRougeData", "周度游历");
        }

        var rouge = KuroClient.Get(root, "rougeData");
        if (rouge.ValueKind == JsonValueKind.Object)
        {
            return ReadResource(root, "rougeData", "周度游历");
        }

        return new WutheringWavesResourceStatus("周度游历", 0, 6000);
    }

    private async Task<WutheringWavesResourceStatus> ReadWeeklyVoyageFromBaseDataAsync(
        SklandCredential credential,
        ArknightsPlayerBinding player,
        WutheringWavesResourceStatus fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await _client.PostFormAsync(
                credential,
                "/aki/roleBox/akiBox/baseData",
                new Dictionary<string, string>
                {
                    ["gameId"] = GameId.ToString(),
                    ["roleId"] = player.Uid,
                    ["serverId"] = player.ChannelMasterId
                },
                cancellationToken,
                browserLike: true);

            var dataText = KuroClient.ReadString(document.RootElement, "data");
            if (string.IsNullOrWhiteSpace(dataText))
            {
                return fallback;
            }

            using var dataDocument = JsonDocument.Parse(dataText);
            var data = dataDocument.RootElement;
            var maximum = KuroClient.ReadInt(data, "rougeScoreLimit") ?? 0;
            if (maximum <= 0)
            {
                return fallback;
            }

            var current = KuroClient.ReadInt(data, "rougeScore") ?? fallback.Current;
            var name = KuroClient.ReadString(data, "rougeTitle") ?? fallback.Name;
            return new WutheringWavesResourceStatus(
                string.IsNullOrWhiteSpace(name) ? "周度游历" : name,
                current,
                maximum,
                fallback.RefreshAt,
                fallback.ExpireAt,
                fallback.Value);
        }
        catch
        {
            return fallback;
        }
    }

    private static WutheringWavesResourceStatus? TryReadBattlePassEntry(
        JsonElement root,
        string fallbackName,
        Func<string, bool> predicate)
    {
        var battlePassData = KuroClient.Get(root, "battlePassData");
        if (battlePassData.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in battlePassData.EnumerateArray())
        {
            var name = KuroClient.ReadString(item, "name") ?? string.Empty;
            if (predicate(name))
            {
                return new WutheringWavesResourceStatus(
                    fallbackName,
                    KuroClient.ReadInt(item, "cur") ?? 0,
                    KuroClient.ReadInt(item, "total") ?? 0,
                    ReadTimestamp(item, "refreshTimeStamp"),
                    ReadTimestamp(item, "expireTimeStamp"),
                    KuroClient.ReadString(item, "value") ?? string.Empty);
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadFinalBattleTime(JsonElement data)
    {
        foreach (var name in new[]
        {
            "finalMatrixData",
            "endlessMatrixData",
            "matrixData",
            "finalBattleData",
            "phantomBattleData",
            "bossData",
            "endBattleData"
        })
        {
            var value = KuroClient.Get(data, name);
            var time = ReadBestTime(value);
            if (time is not null)
            {
                return time;
            }
        }

        return FindNamedTime(data);
    }

    private static DateTimeOffset? FindNamedTime(JsonElement element, string? parentName = null)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var objectName = KuroClient.ReadString(element, "name", "title", "timePreDesc");
            if (LooksLikeFinalMatrix(parentName) || LooksLikeFinalMatrix(objectName))
            {
                var time = ReadBestTime(element);
                if (time is not null)
                {
                    return time;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var time = FindNamedTime(property.Value, property.Name);
                if (time is not null)
                {
                    return time;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var time = FindNamedTime(item, parentName);
                if (time is not null)
                {
                    return time;
                }
            }
        }

        return null;
    }

    private static bool LooksLikeFinalMatrix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("终焉", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("矩阵", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("matrix", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ReadBestTime(JsonElement root)
    {
        foreach (var propertyName in new[]
        {
            "expireTimeStamp",
            "endTimeStamp",
            "endTimestamp",
            "endTime",
            "endAt",
            "refreshTimeStamp"
        })
        {
            var time = ReadTimestamp(root, propertyName);
            if (time is not null)
            {
                return time;
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement root, string propertyName)
    {
        var timestamp = KuroClient.ReadLong(root, propertyName);
        return TimestampToDateTime(timestamp);
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement root, string childName, string propertyName)
    {
        return ReadTimestamp(KuroClient.Get(root, childName), propertyName);
    }

    private static DateTimeOffset? TimestampToDateTime(long? timestamp)
    {
        if (timestamp is null or <= 0)
        {
            return null;
        }

        return timestamp.Value > 10_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).ToLocalTime()
            : DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).ToLocalTime();
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
