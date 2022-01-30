using System;
using System.Threading.Tasks;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;
using GoogleBot.Services;

namespace GoogleBot.Interactions.Modules;

public class MajorityWatchModule : InteractionModuleBase
{
    [LinkComponentInteraction("majority-vote-*")]
    public async Task MajorityVote(SocketMessageComponent component)
    {
        // Console.WriteLine("In MV  GC: " + Context.GuildConfig);
        PreconditionWatcher watcher = Context.GuildConfig.GetWatcher(component.Data.CustomId);
        await component.DeferAsync();

        // Console.WriteLine("In MV " + watcher);

        if (watcher != null)
        {
            _ = watcher.TryVote(component);
        }
    }
}