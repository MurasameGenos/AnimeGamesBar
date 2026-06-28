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
        var status = new WutheringWavesAccountStatus(
            PlayerName: KuroClient.ReadString(data, "roleName") ?? player.NickName,
            ServerName: KuroClient.ReadString(data, "serverName") ?? player.ServerName,
            Waveplates: ReadResource(data, "energyData", "结晶波片"),
            CrystalSolvent: ReadResource(data, "storeEnergyData", "结晶单质"),
            DailyActivity: ReadResource(data, "livenessData", "每日活跃度"),
            WeeklyVoyage: ReadResource(data, "weeklyRougeData", "周度游历"),
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
        var battlePassData = KuroClient.Get(root, "battlePassData");
        if (battlePassData.ValueKind != JsonValueKind.Array)
        {
            return new WutheringWavesResourceStatus("先约电台等级", 0, 0);
        }

        foreach (var item in battlePassData.EnumerateArray())
        {
            var name = KuroClient.ReadString(item, "name") ?? string.Empty;
            if (name.Contains("等级", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("电台", StringComparison.OrdinalIgnoreCase))
            {
                return new WutheringWavesResourceStatus(
                    "先约电台等级",
                    KuroClient.ReadInt(item, "cur") ?? 0,
                    KuroClient.ReadInt(item, "total") ?? 0,
                    ReadTimestamp(item, "refreshTimeStamp"),
                    ReadTimestamp(item, "expireTimeStamp"),
                    KuroClient.ReadString(item, "value") ?? string.Empty);
            }
        }

        var first = battlePassData.EnumerateArray().FirstOrDefault();
        return new WutheringWavesResourceStatus(
            "先约电台等级",
            KuroClient.ReadInt(first, "cur") ?? 0,
            KuroClient.ReadInt(first, "total") ?? 0);
    }

    private static DateTimeOffset? ReadFinalBattleTime(JsonElement data)
    {
        foreach (var name in new[] { "finalBattleData", "phantomBattleData", "bossData", "endBattleData" })
        {
            var value = KuroClient.Get(data, name);
            var time = ReadTimestamp(value, "expireTimeStamp") ?? ReadTimestamp(value, "refreshTimeStamp");
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

        return DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).ToLocalTime();
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
