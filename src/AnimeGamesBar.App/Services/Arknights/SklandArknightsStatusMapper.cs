using System.Text.Json;
using AnimeGamesBar.App.Models;

namespace AnimeGamesBar.App.Services.Arknights;

public sealed class SklandArknightsStatusMapper
{
    private const int SecondsPerSanity = 360;
    private readonly DateTimeOffset _now;

    public SklandArknightsStatusMapper(DateTimeOffset now)
    {
        _now = now;
    }

    public string ReadDoctorName(JsonElement data, string fallback)
    {
        return JsonElementNavigator.ReadString(JsonElementNavigator.Get(data, "status"), "name", "nickName", "nickname") ??
            JsonElementNavigator.ReadString(data, "nickName", "nickname", "name", "doctorName") ??
            fallback;
    }

    public ResourceMeter ReadSanity(JsonElement data)
    {
        var ap = JsonElementNavigator.Get(data, "status", "ap");
        if (ap.ValueKind == JsonValueKind.Object)
        {
            var maximum = JsonElementNavigator.ReadInt(ap, "max", "maximum", "limit");
            var current = JsonElementNavigator.ReadInt(ap, "current", "value", "count");
            var completeRecoveryTime = JsonElementNavigator.ReadLong(ap, "completeRecoveryTime", "fullRecoveryTime", "fullAt");

            if (maximum is not null || current is not null)
            {
                var fullAt = ToDateTime(completeRecoveryTime);
                return new ResourceMeter(
                    CalculateCurrentFromFullAt(current ?? 0, maximum ?? Math.Max(current ?? 0, 0), fullAt, SecondsPerSanity),
                    maximum ?? Math.Max(current ?? 0, 0),
                    fullAt);
            }
        }

        return ReadResource(data, "ap", "sanity", "\u7406\u667A");
    }

    public ResourceMeter ReadDrones(JsonElement data)
    {
        var labor = JsonElementNavigator.Get(data, "building", "labor");
        if (labor.ValueKind == JsonValueKind.Object)
        {
            var maximum = JsonElementNavigator.ReadInt(labor, "maxValue", "max", "maximum", "limit");
            var value = JsonElementNavigator.ReadInt(labor, "value", "current", "count");
            var remainSeconds = JsonElementNavigator.ReadInt(labor, "remainSecs", "remainingSeconds", "recoverTime");
            var lastUpdateTime = JsonElementNavigator.ReadLong(labor, "lastUpdateTime", "lastApAddTime");
            var fullAt = AddSecondsFrom(lastUpdateTime, remainSeconds) ?? AddSeconds(remainSeconds);

            if (maximum is not null || value is not null)
            {
                return new ResourceMeter(
                    CalculateCurrentFromRecovery(value ?? 0, maximum ?? Math.Max(value ?? 0, 0), lastUpdateTime, remainSeconds),
                    maximum ?? Math.Max(value ?? 0, 0),
                    fullAt);
            }
        }

        return ReadResource(data, "labor", "drone", "\u65E0\u4EBA\u673A");
    }

    public WeeklyProgress ReadCampaign(JsonElement data, int maximum)
    {
        var reward = JsonElementNavigator.Get(data, "campaign", "reward");
        return ReadProgressNode(reward, maximum, ReadRefreshAt(JsonElementNavigator.Get(data, "campaign")) ?? NextMondayAtFour()) ??
            ReadWeeklyProgress(data, maximum, NextMondayAtFour(), "campaign", "annihilation", "\u527F\u706D");
    }

    public WeeklyProgress ReadTower(JsonElement data, int maximum)
    {
        var higherItem = JsonElementNavigator.Get(data, "tower", "reward", "higherItem");
        var refreshAt = ReadRefreshAt(JsonElementNavigator.Get(data, "tower", "reward")) ??
            ReadRefreshAt(JsonElementNavigator.Get(data, "tower"));

        return ReadProgressNode(higherItem, maximum, refreshAt) ??
            ReadWeeklyProgress(data, maximum, refreshAt, "tower", "sss", "\u4FDD\u5168");
    }

    public WeeklyProgress ReadTowerStrips(JsonElement data, int maximum)
    {
        var reward = JsonElementNavigator.Get(data, "tower", "reward");
        var lowerItem = JsonElementNavigator.Get(reward, "lowerItem");
        var refreshAt = ReadRefreshAt(reward) ?? ReadRefreshAt(JsonElementNavigator.Get(data, "tower"));

        return ReadProgressNode(lowerItem, maximum, refreshAt) ?? WeeklyProgress.Empty(maximum) with { RefreshAt = refreshAt };
    }

    public BuildingStatus ReadBuildingStatus(JsonElement data)
    {
        var building = JsonElementNavigator.Get(data, "building");
        if (building.ValueKind != JsonValueKind.Object)
        {
            return BuildingStatus.Empty;
        }

        return new BuildingStatus(
            ReadOrderProgress(JsonElementNavigator.Get(building, "tradings")),
            ReadManufactureProgress(
                JsonElementNavigator.Get(building, "manufactures"),
                JsonElementNavigator.Get(data, "manufactureFormulaInfoMap")),
            ReadArrayLength(JsonElementNavigator.Get(building, "tiredChars")));
    }

    public ResourceMeter ReadResource(JsonElement data, params string[] aliases)
    {
        foreach (var node in JsonElementNavigator.FindObjectsByNameHints(data, aliases))
        {
            var current = JsonElementNavigator.ReadInt(node, "current", "value", "count", "now");
            var maximum = JsonElementNavigator.ReadInt(node, "max", "maxValue", "maximum", "limit", "total");
            var secondsToFull = JsonElementNavigator.ReadInt(node, "recoverTime", "remainSecs", "completeSeconds", "fullRecoveryTime");
            var fullAtSeconds = JsonElementNavigator.ReadLong(node, "fullAt", "completeAt", "ts");
            var lastUpdateSeconds = JsonElementNavigator.ReadLong(node, "lastUpdateTime", "lastApAddTime");

            if (current is not null || maximum is not null)
            {
                return new ResourceMeter(
                    current ?? 0,
                    maximum ?? Math.Max(current ?? 0, 0),
                    ToDateTime(fullAtSeconds) ?? AddSecondsFrom(lastUpdateSeconds, secondsToFull) ?? AddSeconds(secondsToFull));
            }
        }

        return ResourceMeter.Empty;
    }

    public TrainingRoomStatus ReadTraining(JsonElement data)
    {
        var training = JsonElementNavigator.Get(data, "building", "training");
        if (training.ValueKind == JsonValueKind.Object)
        {
            var trainee = JsonElementNavigator.Get(training, "trainee");
            var targetSkill = JsonElementNavigator.ReadInt(trainee, "targetSkill");
            var remainingSeconds = JsonElementNavigator.ReadInt(training, "remainSecs", "remainingSeconds", "leftSecs");

            if (trainee.ValueKind == JsonValueKind.Object && targetSkill is >= 0)
            {
                var charId = JsonElementNavigator.ReadString(trainee, "charId");
                var operatorName = ReadCharacterName(data, charId) ?? charId ?? "\u8BAD\u7EC3\u5E72\u5458";
                var skillName = $"\u6280\u80FD {targetSkill.Value + 1}";
                return new TrainingRoomStatus(true, operatorName, skillName, null, AddSeconds(remainingSeconds));
            }
        }

        foreach (var node in JsonElementNavigator.FindObjectsByNameHints(data, "training", "train", "\u8BAD\u7EC3"))
        {
            var operatorName = JsonElementNavigator.ReadString(node, "charName", "operatorName", "name", "characterName");
            var skillName = JsonElementNavigator.ReadString(node, "skillName", "skill", "skillId") ?? "-";
            var targetLevel = JsonElementNavigator.ReadInt(node, "targetLevel", "targetSkillLevel", "level");
            var remainingSeconds = JsonElementNavigator.ReadInt(node, "remainSecs", "remainingSeconds", "leftSecs", "completeSeconds");
            var completeAt = ToDateTime(JsonElementNavigator.ReadLong(node, "completeAt", "finishAt", "endTs")) ?? AddSeconds(remainingSeconds);

            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                return new TrainingRoomStatus(true, operatorName, skillName, targetLevel, completeAt);
            }
        }

        return TrainingRoomStatus.Empty;
    }

    public WeeklyProgress ReadWeeklyProgress(
        JsonElement data,
        int maximum,
        DateTimeOffset? refreshAt = null,
        params string[] aliases)
    {
        foreach (var node in JsonElementNavigator.FindObjectsByNameHints(data, aliases))
        {
            var current = JsonElementNavigator.ReadInt(node, "current", "value", "count", "reward", "score");
            var max = JsonElementNavigator.ReadInt(node, "max", "maximum", "limit", "total") ?? maximum;

            if (current is not null)
            {
                return new WeeklyProgress(Math.Clamp(current.Value, 0, max), max, refreshAt ?? ReadRefreshAt(node));
            }
        }

        return WeeklyProgress.Empty(maximum) with { RefreshAt = refreshAt };
    }

    private static WeeklyProgress? ReadProgressNode(JsonElement node, int fallbackMaximum, DateTimeOffset? refreshAt = null)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var current = JsonElementNavigator.ReadInt(node, "current", "value", "count", "reward", "score");
        var maximum = JsonElementNavigator.ReadInt(node, "total", "max", "maximum", "limit") ?? fallbackMaximum;
        return current is null ? null : new WeeklyProgress(Math.Clamp(current.Value, 0, maximum), maximum, refreshAt);
    }

    private ProgressStatus ReadOrderProgress(JsonElement tradings)
    {
        if (tradings.ValueKind != JsonValueKind.Array)
        {
            return ProgressStatus.Empty;
        }

        var current = 0;
        var maximum = 0;
        DateTimeOffset? nextCompleteAt = null;

        foreach (var trading in tradings.EnumerateArray())
        {
            var stock = ReadArrayLength(JsonElementNavigator.Get(trading, "stock"));
            var stockLimit = JsonElementNavigator.ReadInt(trading, "stockLimit", "limit", "max") ?? stock;
            var completeAt = ToDateTime(JsonElementNavigator.ReadLong(trading, "completeWorkTime", "completeAt", "finishAt"));

            current += stock;
            maximum += Math.Max(stockLimit, stock);

            if (completeAt is not null && completeAt > _now)
            {
                current += 1;
                nextCompleteAt = Min(nextCompleteAt, completeAt);
            }
        }

        return new ProgressStatus(Math.Clamp(current, 0, Math.Max(maximum, current)), maximum, nextCompleteAt);
    }

    private ProgressStatus ReadManufactureProgress(JsonElement manufactures, JsonElement formulaInfoMap)
    {
        if (manufactures.ValueKind != JsonValueKind.Array)
        {
            return ProgressStatus.Empty;
        }

        var current = 0;
        var maximum = 0;
        DateTimeOffset? nextCompleteAt = null;

        foreach (var manufacture in manufactures.EnumerateArray())
        {
            var formulaId = JsonElementNavigator.ReadString(manufacture, "formulaId");
            var formula = string.IsNullOrWhiteSpace(formulaId)
                ? default
                : JsonElementNavigator.Get(formulaInfoMap, formulaId);
            var weight = Math.Max(
                JsonElementNavigator.ReadInt(formula, "weight") ??
                JsonElementNavigator.ReadInt(manufacture, "weight") ??
                1,
                1);
            var capacity = JsonElementNavigator.ReadInt(manufacture, "capacity") ?? 0;
            var slotMaximum = Math.Max(capacity / weight, 0);
            var completeAt = ToDateTime(JsonElementNavigator.ReadLong(manufacture, "completeWorkTime", "completeAt", "finishAt"));
            var complete = Math.Max(JsonElementNavigator.ReadInt(manufacture, "complete", "current") ?? 0, 0);

            maximum += slotMaximum;

            if (completeAt is not null && completeAt <= _now)
            {
                current += slotMaximum;
                continue;
            }

            nextCompleteAt = Min(nextCompleteAt, completeAt);
            current += EstimateManufactureComplete(manufacture, formula, complete, slotMaximum);
        }

        return new ProgressStatus(Math.Clamp(current, 0, Math.Max(maximum, current)), maximum, nextCompleteAt);
    }

    private int EstimateManufactureComplete(JsonElement manufacture, JsonElement formula, int complete, int slotMaximum)
    {
        if (slotMaximum <= 0)
        {
            return 0;
        }

        var costPoint = JsonElementNavigator.ReadDouble(formula, "costPoint", "cost") ?? 0;
        var speed = JsonElementNavigator.ReadDouble(manufacture, "speed") ?? 0;
        var lastUpdateAt = ToDateTime(JsonElementNavigator.ReadLong(manufacture, "lastUpdateTime", "lastCompleteTime"));

        if (costPoint > 0 && speed > 0 && lastUpdateAt is not null && _now > lastUpdateAt)
        {
            var secondsPerItem = costPoint / speed;
            if (secondsPerItem > 0)
            {
                complete += Math.Max((int)Math.Floor((_now - lastUpdateAt.Value).TotalSeconds / secondsPerItem), 0);
            }
        }

        return Math.Clamp(complete, 0, slotMaximum);
    }

    private static int ReadArrayLength(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Array ? element.GetArrayLength() : 0;
    }

    private DateTimeOffset? ReadRefreshAt(JsonElement node)
    {
        return ToDateTime(JsonElementNavigator.ReadLong(
            node,
            "termTs",
            "refreshTs",
            "refreshTime",
            "nextRefreshTs",
            "nextRefreshTime",
            "endTs"));
    }

    private DateTimeOffset NextMondayAtFour()
    {
        var localNow = _now.ToLocalTime();
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)localNow.DayOfWeek + 7) % 7;
        var next = new DateTimeOffset(localNow.Date.AddDays(daysUntilMonday).AddHours(4), localNow.Offset);
        return next <= localNow ? next.AddDays(7) : next;
    }

    private static DateTimeOffset? Min(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return left <= right ? left : right;
    }

    private string? ReadCharacterName(JsonElement data, string? charId)
    {
        if (string.IsNullOrWhiteSpace(charId))
        {
            return null;
        }

        var charInfoMap = JsonElementNavigator.Get(data, "charInfoMap");
        if (charInfoMap.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var character = JsonElementNavigator.Get(charInfoMap, charId);
        return JsonElementNavigator.ReadString(character, "name", "charName", "operatorName");
    }

    private int CalculateCurrentFromFullAt(int current, int maximum, DateTimeOffset? fullAt, int secondsPerPoint)
    {
        if (maximum <= 0 || fullAt is null)
        {
            return Math.Clamp(current, 0, Math.Max(maximum, current));
        }

        if (fullAt <= _now)
        {
            return Math.Clamp(maximum, 0, Math.Max(maximum, current));
        }

        var remainingPoints = (int)Math.Ceiling((fullAt.Value - _now).TotalSeconds / secondsPerPoint);
        return Math.Clamp(maximum - remainingPoints, 0, maximum);
    }

    private int CalculateCurrentFromRecovery(int value, int maximum, long? lastUpdateTime, int? remainSeconds)
    {
        if (maximum <= 0 || value >= maximum || remainSeconds is null or <= 0)
        {
            return Math.Clamp(value, 0, Math.Max(maximum, value));
        }

        var updatedAt = ToDateTime(lastUpdateTime);
        if (updatedAt is null)
        {
            return Math.Clamp(value, 0, maximum);
        }

        var pointsToFull = maximum - value;
        if (pointsToFull <= 0)
        {
            return maximum;
        }

        var secondsPerPoint = remainSeconds.Value / (double)pointsToFull;
        if (secondsPerPoint <= 0)
        {
            return Math.Clamp(value, 0, maximum);
        }

        var gained = (int)Math.Floor((_now - updatedAt.Value).TotalSeconds / secondsPerPoint);
        return Math.Clamp(value + gained, 0, maximum);
    }

    private DateTimeOffset? AddSeconds(int? seconds)
    {
        return seconds is > 0 ? _now.AddSeconds(seconds.Value) : null;
    }

    private static DateTimeOffset? AddSecondsFrom(long? timestamp, int? seconds)
    {
        var time = ToDateTime(timestamp);
        return time is not null && seconds is > 0 ? time.Value.AddSeconds(seconds.Value) : null;
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
