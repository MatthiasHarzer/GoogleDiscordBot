using System.Linq;
using System.Threading.Tasks;
using Discord;
using GoogleBot.Exceptions;

namespace GoogleBot.Interactions.Preconditions;

/// <summary>
/// Requires that the bot is alone in a VC or isn't connected at all
/// </summary>
public class RequiresEmptyBotChannel : PreconditionAttribute
{
    public override async Task Satisfy()
    {
        IVoiceChannel? voiceChannel = Context.GuildConfig.BotsVoiceChannel;
        if (voiceChannel != null)
        {
            var users = await voiceChannel.GetUsersAsync().ToListAsync().AsTask();
            int userCount = users.First()?.ToList().FindAll(u => !u.IsBot).Count ?? 0;
            if (userCount > 0)
            {
                throw new PreconditionNotSatisfiedException(Responses.BotsVcNotEmpty(voiceChannel));
            }
        } 
    }
}