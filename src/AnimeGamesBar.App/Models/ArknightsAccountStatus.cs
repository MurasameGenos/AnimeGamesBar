namespace AnimeGamesBar.App.Models;

public sealed record ArknightsAccountStatus(
    string DoctorName,
    string ChannelName,
    ResourceMeter Sanity,
    ResourceMeter Drones,
    TrainingRoomStatus TrainingRoom,
    WeeklyProgress Annihilation,
    WeeklyProgress SecurityService,
    DateTimeOffset UpdatedAt);
