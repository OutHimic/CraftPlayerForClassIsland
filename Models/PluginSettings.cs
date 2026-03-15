namespace CraftPlayer.Models;

public class PluginSettings
{
    public bool EnableSmtc { get; set; } = true;
    public List<Playlist> Playlists { get; set; } = [];
}
