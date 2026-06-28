namespace AnimeGamesBar.App.Models;

public sealed record ArknightsAccountStatus(
    string DoctorName,
    string ChannelName,
    ResourceMeter Sanity,
    ResourceMeter Drones,
    TrainingRoomStatus TrainingRoom,
    BuildingStatus Building,
    WeeklyProgress Annihilation,
    WeeklyProgress SecurityService,
    WeeklyProgress SecurityServiceStrips,
    DateTimeOffset UpdatedAt);
