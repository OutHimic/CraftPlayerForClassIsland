using Microsoft.Extensions.Hosting;

namespace CraftPlayer.Services.Storage;

public class InitializeSettingsHostedService(SettingsStore settingsStore, ClassIsland.Core.Abstractions.PluginBase pluginBase) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        settingsStore.Initialize(pluginBase.PluginConfigFolder);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
