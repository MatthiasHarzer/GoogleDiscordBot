﻿using System.Threading.Tasks;
using GoogleBot.Interactions.CustomAttributes;

namespace GoogleBot.Interactions.Modules;

public class MajorityWatchModule : InteractionModuleBase
{
    [LinkComponentInteraction("vote-*")]
    public async Task MajorityVote()
    {
        
        // Console.WriteLine("In MV  GC: " + Context.GuildConfig);
        // PreconditionWatcher watcher = Context.GuildConfig.GetWatcher(Component.Data.CustomId)!;
        await Context.GuildConfig.VoteService.TryVote(Component);
        await Component.DeferAsync();

    }
    
    [LinkComponentInteraction("next-q-page-*")]
    public async Task OnQueueNextPageClicked()
    {
        if (Context.DataStore.QueuePage > Context.GuildConfig.AudioPlayer.QueuePages.Length - 1)
        {
            Context.DataStore.QueuePage = Context.GuildConfig.AudioPlayer.QueuePages.Length - 1;
        }
        if (Context.DataStore.QueuePage < Context.GuildConfig.AudioPlayer.QueuePages.Length - 1)
        {
            Context.DataStore.QueuePage++;
            
        }
        await Component.Message.ModifyAsync(properties =>
        {
            properties.Embed = Responses.QueuePage(Context.GuildConfig.AudioPlayer, Context.DataStore.QueuePage).BuiltEmbed;
        } );
        
        await Component.DeferAsync();
    }
    
    [LinkComponentInteraction("prev-q-page-*")]
    public async Task OnQueuePreviousPageClicked()
    {
        if (Context.DataStore.QueuePage > 0)
        {
            Context.DataStore.QueuePage--;
            if (Context.DataStore.QueuePage >= Context.GuildConfig.AudioPlayer.QueuePages.Length - 1)
                Context.DataStore.QueuePage = 0;
        }
        
        await Component.Message.ModifyAsync(properties =>
        {
            properties.Embed = Responses.QueuePage(Context.GuildConfig.AudioPlayer, Context.DataStore.QueuePage).BuiltEmbed;
        } );
        
        await Component.DeferAsync();
    }
}