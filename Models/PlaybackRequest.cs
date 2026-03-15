namespace CraftPlayer.Models;

public class PlaybackRequest
{
    public PlaybackSourceType SourceType { get; set; } = PlaybackSourceType.Playlist;
    public string PlaylistId { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int PlayCount { get; set; } = 1;
    public int PerTrackLimitSeconds { get; set; } = 0;
    public int TotalLimitSeconds { get; set; } = 0;
    public PlaybackOrderMode OrderMode { get; set; } = PlaybackOrderMode.Sequential;
}
