using System.Threading.Tasks;
using Discord;
using GoogleBot.Exceptions;

namespace GoogleBot.Interactions.Preconditions;

/// <summary>
/// The command requires the majority of the voice channels users to agree
/// </summary>
public class RequiresMajority : PreconditionAttribute
{
    public override async Task Satisfy()
    {
        IVoiceChannel? voiceChannel = Context.GuildConfig.BotsVoiceChannel;
        if (Context.VoiceChannel == null && voiceChannel != null)
            throw new PreconditionNotSatisfiedException(
                Responses.WrongVoiceChannel(voiceChannel));
        bool voteResult = await Context.GuildConfig.VoteService.AwaitMajorityCommandVote(Context);
        // Console.WriteLine("Vote result: " + voteResult);
        if (!voteResult)
        {
            throw new PreconditionFailedException("Majority vote failed", true);
        }
   
    }
}