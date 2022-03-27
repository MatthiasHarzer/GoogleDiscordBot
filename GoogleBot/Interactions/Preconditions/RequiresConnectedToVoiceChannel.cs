using System.Threading.Tasks;
using Discord;
using GoogleBot.Interactions.Preconditions.Exceptions;

namespace GoogleBot.Interactions.Preconditions;

/// <summary>
/// The user must be connected to a voice channel
/// </summary>
public class RequiresConnectedToVoiceChannel : PreconditionAttribute
{
    public override Task Satisfy()
    {
        IVoiceChannel? vc = Context.User.VoiceChannel;

        if (vc == null)
        {
            throw new PreconditionNotSatisfiedException(Responses.NoVoiceChannel());
        }
        return Task.CompletedTask;
    }
}