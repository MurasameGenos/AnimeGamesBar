namespace AnimeGamesBar.App.Models;

public sealed record WutheringWavesAccountStatus(
    string PlayerName,
    string ServerName,
    WutheringWavesResourceStatus Waveplates,
    WutheringWavesResourceStatus CrystalSolvent,
    WutheringWavesResourceStatus DailyActivity,
    WutheringWavesResourceStatus WeeklyVoyage,
    WutheringWavesResourceStatus WeeklyBoss,
    WutheringWavesResourceStatus BattlePassLevel,
    DateTimeOffset? TowerResetAt,
    DateTimeOffset? SeaResetAt,
    DateTimeOffset? FinalBattleEndAt,
    bool HasSignedIn,
    DateTimeOffset UpdatedAt);
