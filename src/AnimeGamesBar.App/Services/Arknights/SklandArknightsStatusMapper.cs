using System.Text.Json;
using AnimeGamesBar.App.Models;

namespace AnimeGamesBar.App.Services.Arknights;

public sealed class SklandArknightsStatusMapper
{
    private readonly DateTimeOffset _now;

    public SklandArknightsStatusMapper(DateTimeOffset now)
    {
        _now = now;
    }

    public string ReadDoctorName(JsonElement data, string fallback)
    {
        return JsonElementNavigator.ReadString(data, "nickName", "nickname", "name", "doctorName") ?? fallback;
    }

    public ResourceMeter ReadResource(JsonElement data, params string[] aliases)
    {
        foreach (var node in JsonElementNavigator.FindObjectsByNameHints(data, aliases))
        {
            var current = JsonElementNavigator.ReadInt(node, "current", "value", "count", "now");
            var maximum = JsonElementNavigator.ReadInt(node, "max", "maximum", "limit", "total");
            var secondsToFull = JsonElementNavigator.ReadInt(node, "recoverTime", "remainSecs", "completeSeconds", "fullRecoveryTime");
            var fullAtSeconds = JsonElementNavigator.ReadLong(node, "fullAt", "completeAt", "ts");

            if (current is not null || maximum is not null)
            {
                return new ResourceMeter(
                    current ?? 0,
                    maximum ?? Math.Max(current ?? 0, 0),
                    ToDateTime(fullAtSeconds) ?? AddSeconds(secondsToFull));
            }
        }

        return ResourceMeter.Empty;
    }

    public TrainingRoomStatus ReadTraining(JsonElement data)
    {
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

    public WeeklyProgress ReadWeeklyProgress(JsonElement data, int maximum, params string[] aliases)
    {
        foreach (var node in JsonElementNavigator.FindObjectsByNameHints(data, aliases))
        {
            var current = JsonElementNavigator.ReadInt(node, "current", "value", "count", "reward", "score");
            var max = JsonElementNavigator.ReadInt(node, "max", "maximum", "limit", "total") ?? maximum;

            if (current is not null)
            {
                return new WeeklyProgress(Math.Clamp(current.Value, 0, max), max);
            }
        }

        return WeeklyProgress.Empty(maximum);
    }

    private DateTimeOffset? AddSeconds(int? seconds)
    {
        return seconds is > 0 ? _now.AddSeconds(seconds.Value) : null;
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
