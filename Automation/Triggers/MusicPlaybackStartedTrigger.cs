using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using CraftPlayer.Services.Automation;

namespace CraftPlayer.Automation.Triggers;

[TriggerInfo("cn.craftine.craftplayer.trigger.session-started", "音乐播放开始")]
public class MusicPlaybackStartedTrigger(PlaybackAutomationBridge bridge) : TriggerBase
{
    public override void Loaded()
    {
        bridge.SessionStarted += BridgeOnSessionStarted;
    }

    public override void UnLoaded()
    {
        bridge.SessionStarted -= BridgeOnSessionStarted;
    }

    void BridgeOnSessionStarted(object? sender, EventArgs e)
    {
        Trigger();
    }
}
