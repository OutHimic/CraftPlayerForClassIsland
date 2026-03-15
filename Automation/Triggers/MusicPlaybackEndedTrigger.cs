using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using CraftPlayer.Services.Automation;

namespace CraftPlayer.Automation.Triggers;

[TriggerInfo("cn.craftine.craftplayer.trigger.session-ended", "音乐播放结束")]
public class MusicPlaybackEndedTrigger(PlaybackAutomationBridge bridge) : TriggerBase
{
    public override void Loaded()
    {
        bridge.SessionEnded += BridgeOnSessionEnded;
    }

    public override void UnLoaded()
    {
        bridge.SessionEnded -= BridgeOnSessionEnded;
    }

    void BridgeOnSessionEnded(object? sender, EventArgs e)
    {
        Trigger();
    }
}
