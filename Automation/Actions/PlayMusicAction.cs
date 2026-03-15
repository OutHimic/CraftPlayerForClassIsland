using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using CraftPlayer.Automation.Actions.Settings;
using CraftPlayer.Models;
using CraftPlayer.Services.Playback;

namespace CraftPlayer.Automation.Actions;

[ActionInfo("cn.craftine.craftplayer.play", "播放音乐", "\uE768")]
public class PlayMusicAction(PlaybackEngineService playbackEngineService) : ActionBase<PlayMusicActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        if (Settings.SourceType == null)
        {
            return;
        }

        if (Settings.SourceType == PlaybackSourceType.File && string.IsNullOrWhiteSpace(Settings.FilePath))
        {
            return;
        }

        if (Settings.SourceType == PlaybackSourceType.File && !Path.IsPathRooted(Settings.FilePath))
        {
            return;
        }

        if (Settings.SourceType == PlaybackSourceType.Playlist && string.IsNullOrWhiteSpace(Settings.PlaylistId))
        {
            return;
        }

        var request = new PlaybackRequest
        {
            SourceType = Settings.SourceType.Value,
            PlaylistId = Settings.PlaylistId,
            FilePath = Settings.FilePath,
            PlayCount = Settings.PlayCount,
            PerTrackLimitSeconds = Settings.PerTrackLimitSeconds,
            TotalLimitSeconds = Settings.TotalLimitSeconds,
            OrderMode = Settings.OrderMode
        };
        await playbackEngineService.StartAsync(request);
    }
}
