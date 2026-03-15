using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using CraftPlayer.Automation.Actions;
using CraftPlayer.Automation.Actions.SettingsControls;
using CraftPlayer.Automation.Rules;
using CraftPlayer.Automation.Triggers;
using CraftPlayer.Services.Automation;
using CraftPlayer.Services.Export;
using CraftPlayer.Services.Playback;
using CraftPlayer.Services.Storage;
using CraftPlayer.ViewModels;
using CraftPlayer.Views.SettingsPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CraftPlayer;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var store = new SettingsStore();
            store.Initialize(PluginConfigFolder);
            return store;
        });
        services.AddSingleton<LibraryFileService>();
        services.AddSingleton<PlaybackMetadataService>();
        services.AddSingleton<SmtcBridgeService>();
        services.AddSingleton<PlaybackAutomationBridge>();
        services.AddSingleton<PlaybackEngineService>();
        services.AddSingleton<PlaylistCsvExportService>();
        services.AddSingleton<CraftPlayerSettingsViewModel>();
        services.AddHostedService<PlaybackHostedService>();

        services.AddAction<PlayMusicAction, PlayMusicActionSettingsControl>();
        services.AddAction<StopMusicAction>();
        services.AddTrigger<MusicPlaybackStartedTrigger>();
        services.AddTrigger<MusicPlaybackEndedTrigger>();
        services.AddTrigger<TrackPlaybackEndedTrigger>();
        PlaybackRuleRegistration.Register(services);
        services.AddSettingsPage<CraftPlayerSettingsPage>();
    }
}
