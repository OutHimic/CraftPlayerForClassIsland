using CraftPlayer.Models;

namespace CraftPlayer.Services.Automation;

public class PlaybackAutomationBridge
{
    public event EventHandler? SessionStarted;
    public event EventHandler? SessionEnded;
    public event EventHandler? TrackEnded;
    public event EventHandler<TrackItem>? TrackStarted;

    public void RaiseSessionStarted() => SessionStarted?.Invoke(this, EventArgs.Empty);
    public void RaiseSessionEnded() => SessionEnded?.Invoke(this, EventArgs.Empty);
    public void RaiseTrackEnded() => TrackEnded?.Invoke(this, EventArgs.Empty);
    public void RaiseTrackStarted(TrackItem track) => TrackStarted?.Invoke(this, track);
}
