using CraftPlayer.Models;
using CraftPlayer.Services.Storage;
using Windows.Media;
using Windows.Media.Playback;

namespace CraftPlayer.Services.Playback;

public class SmtcBridgeService(SettingsStore settingsStore)
{
    const string AppMediaId = "cn.craftine.craftplayer";

    public void UpdateEnabled(MediaPlayer player)
    {
        var enabled = settingsStore.Settings.EnableSmtc;
        player.CommandManager.IsEnabled = enabled;
        player.SystemMediaTransportControls.IsEnabled = enabled;
        if (!enabled)
        {
            var smtc = player.SystemMediaTransportControls;
            smtc.DisplayUpdater.ClearAll();
            smtc.DisplayUpdater.Update();
            smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
        }
    }

    public async Task UpdateNowPlayingAsync(MediaPlayer player, TrackItem track, string absolutePath)
    {
        if (!settingsStore.Settings.EnableSmtc)
        {
            return;
        }

        var smtc = player.SystemMediaTransportControls;
        player.CommandManager.IsEnabled = true;
        smtc.IsEnabled = true;
        smtc.DisplayUpdater.AppMediaId = AppMediaId;
        smtc.DisplayUpdater.ClearAll();

        if (File.Exists(absolutePath))
        {
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(absolutePath);
                await smtc.DisplayUpdater.CopyFromFileAsync(MediaPlaybackType.Music, file);
                smtc.DisplayUpdater.Thumbnail = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file);
            }
            catch
            {
                smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
            }
        }
        else
        {
            smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
        }

        smtc.DisplayUpdater.MusicProperties.Title = string.IsNullOrWhiteSpace(track.Title) ? Path.GetFileNameWithoutExtension(track.FileName) : track.Title;
        smtc.DisplayUpdater.MusicProperties.Artist = track.Artist;
        smtc.DisplayUpdater.MusicProperties.AlbumArtist = track.Artist;
        smtc.DisplayUpdater.Update();
    }

    public void UpdatePlaybackStatus(MediaPlayer player, MediaPlaybackStatus status)
    {
        if (!settingsStore.Settings.EnableSmtc)
        {
            return;
        }

        player.SystemMediaTransportControls.PlaybackStatus = status;
    }

    public void Clear(MediaPlayer player)
    {
        var smtc = player.SystemMediaTransportControls;
        smtc.DisplayUpdater.ClearAll();
        smtc.DisplayUpdater.Update();
        smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
        smtc.IsEnabled = false;
        player.CommandManager.IsEnabled = false;
    }
}
