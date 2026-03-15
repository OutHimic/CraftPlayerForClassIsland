namespace CraftPlayer.Models;

public class Playlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新建歌单";
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string LastPlayedTrackId { get; set; } = "";
    public List<TrackItem> Tracks { get; set; } = [];
}
