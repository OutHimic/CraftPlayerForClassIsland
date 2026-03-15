using CraftPlayer.Services.Playback;

namespace CraftPlayer.Services;

public static class RuntimeContext
{
    public static PlaybackEngineService? PlaybackEngine { get; set; }
}
