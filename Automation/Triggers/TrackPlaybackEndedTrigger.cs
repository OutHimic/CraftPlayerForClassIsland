using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using CraftPlayer.Services.Automation;

namespace CraftPlayer.Automation.Triggers;

[TriggerInfo("cn.craftine.craftplayer.trigger.track-ended", "单曲播放结束")]
public class TrackPlaybackEndedTrigger(PlaybackAutomationBridge bridge) : TriggerBase
{
    public override void Loaded()
    {
        bridge.TrackEnded += BridgeOnTrackEnded;
    }

    public override void UnLoaded()
    {
        bridge.TrackEnded -= BridgeOnTrackEnded;
    }

    void BridgeOnTrackEnded(object? sender, EventArgs e)
    {
        Trigger();
    }
}
