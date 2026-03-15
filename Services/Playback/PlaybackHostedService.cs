using Microsoft.Extensions.Hosting;
using CraftPlayer.Services;

namespace CraftPlayer.Services.Playback;

public class PlaybackHostedService(PlaybackEngineService playbackEngineService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        playbackEngineService.Initialize();
        RuntimeContext.PlaybackEngine = playbackEngineService;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        playbackEngineService.Dispose();
        RuntimeContext.PlaybackEngine = null;
        return Task.CompletedTask;
    }
}
