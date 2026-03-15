using ClassIsland.Core.Extensions.Registry;
using CraftPlayer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CraftPlayer.Automation.Rules;

public static class PlaybackRuleRegistration
{
    public static void Register(IServiceCollection services)
    {
        services.AddRule(
            "cn.craftine.craftplayer.rule.is-playing",
            "正在播放音乐",
            "\uF5E7",
            _ => RuntimeContext.PlaybackEngine?.IsPlaying == true);
    }
}
