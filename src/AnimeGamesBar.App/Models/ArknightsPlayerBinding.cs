namespace AnimeGamesBar.App.Models;

public sealed record ArknightsPlayerBinding(
    string Uid,
    string UserId,
    string NickName,
    string ChannelName,
    string ServerName,
    string ChannelMasterId = "",
    string AppCode = "arknights",
    string RoleId = "")
{
    public string DisplayName => string.IsNullOrWhiteSpace(ServerName)
        ? $"{NickName} ({Uid})"
        : $"{NickName} - {ServerName} ({Uid})";
}
