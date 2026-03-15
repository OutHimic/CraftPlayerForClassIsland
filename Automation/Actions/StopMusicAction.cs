using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using CraftPlayer.Services.Playback;

namespace CraftPlayer.Automation.Actions;

[ActionInfo("cn.craftine.craftplayer.stop", "停止播放", "\uF78A")]
public class StopMusicAction(PlaybackEngineService playbackEngineService) : ActionBase
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        await playbackEngineService.StopAsync();
    }
}
