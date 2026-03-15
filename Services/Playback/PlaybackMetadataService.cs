using CraftPlayer.Models;
using Windows.Storage;

namespace CraftPlayer.Services.Playback;

public class PlaybackMetadataService
{
    public async Task<TrackItem> BuildTrackFromFileAsync(string absolutePath)
    {
        var track = new TrackItem
        {
            FileName = Path.GetFileName(absolutePath),
            Title = Path.GetFileNameWithoutExtension(absolutePath),
            Artist = "",
            Duration = TimeSpan.Zero
        };

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(absolutePath);
            var props = await file.Properties.GetMusicPropertiesAsync();
            if (!string.IsNullOrWhiteSpace(props.Title))
            {
                track.Title = props.Title;
            }

            if (props.Artist?.Any() == true)
            {
                track.Artist = string.Join(", ", props.Artist);
            }

            if (props.Duration > TimeSpan.Zero)
            {
                track.Duration = props.Duration;
            }
        }
        catch
        {
        }

        return track;
    }
}
