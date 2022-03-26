using System.Threading.Tasks;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;
using GoogleBot.Services;

namespace GoogleBot.Interactions.Modules;

public class MajorityWatchModule : InteractionModuleBase
{
    [LinkComponentInteraction("majority-vote-*")]
    public async Task MajorityVote()
    {
        // Console.WriteLine("In MV  GC: " + Context.GuildConfig);
        PreconditionWatcher watcher = Context.GuildConfig.GetWatcher(Component.Data.CustomId)!;
        await Component.DeferAsync();

        // Console.WriteLine("In MV " + watcher);

        if (watcher != null)
        {
            _ = watcher.TryVote(Component);
        }
    }
    
    [LinkComponentInteraction("next-q-page-*")]
    public async Task OnQueueNextPageClicked()
    {
        if (Context.DataStore.QueuePage < Context.GuildConfig.AudioPlayer.QueuePages.Length - 1)
        {
            Context.DataStore.QueuePage++;
            await Component.Message.ModifyAsync(properties =>
            {
                properties.Embed = Responses.QueuePage(Context.GuildConfig.AudioPlayer, Context.DataStore.QueuePage).BuiltEmbed;
            } );
        }
        await Component.DeferAsync();
    }
    
    [LinkComponentInteraction("prev-q-page-*")]
    public async Task OnQueuePreviousPageClicked()
    {
        if (Context.DataStore.QueuePage > 0)
        {
            Context.DataStore.QueuePage--;
            await Component.Message.ModifyAsync(properties =>
            {
                properties.Embed = Responses.QueuePage(Context.GuildConfig.AudioPlayer, Context.DataStore.QueuePage).BuiltEmbed;
            } );
        }
        await Component.DeferAsync();
    }
}