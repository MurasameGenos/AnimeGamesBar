namespace AnimeGamesBar.App.Models;

public sealed record TrainingRoomStatus(
    bool IsTraining,
    string OperatorName,
    string SkillName,
    int? TargetSkillLevel,
    DateTimeOffset? CompleteAt)
{
    public static TrainingRoomStatus Empty { get; } = new(false, "\u7A7A\u95F2", "-", null, null);
}
