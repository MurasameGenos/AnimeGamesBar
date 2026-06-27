namespace AnimeGamesBar.App.Models;

public sealed record ArknightsPlayerBinding(
    string Uid,
    string NickName,
    string ChannelName,
    string ServerName)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ServerName)
        ? $"{NickName} ({Uid})"
        : $"{NickName} - {ServerName} ({Uid})";
}
