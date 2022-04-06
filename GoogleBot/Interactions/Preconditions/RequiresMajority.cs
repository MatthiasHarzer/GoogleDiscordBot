using System.Threading.Tasks;
using GoogleBot.Exceptions;

namespace GoogleBot.Interactions.Preconditions;

/// <summary>
/// The command requires the majority of the voice channels users to agree
/// </summary>
public class RequiresMajority : PreconditionAttribute
{
    public override async Task Satisfy()
    {
        if (Context.VoiceChannel == null && Context.GuildConfig.BotConnectedToVc)
            throw new PreconditionNotSatisfiedException(
                Responses.WrongVoiceChannel(Context.GuildConfig.BotsVoiceChannel!));
        bool voteResult = await Context.GuildConfig.VoteService.AwaitMajorityCommandVote(Context);
        // Console.WriteLine("Vote result: " + voteResult);
        if (!voteResult)
        {
            throw new PreconditionFailedException("Majority vote failed", true);
        }
   
    }
}