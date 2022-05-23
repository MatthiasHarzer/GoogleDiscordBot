using System.Threading.Tasks;
using Discord;
using GoogleBot.Exceptions;

namespace GoogleBot.Interactions.Preconditions;

/// <summary>
/// Bot and user have to be connected to the same voice channel (or both none)
/// </summary>
public class RequiresSameVoiceChannel : PreconditionAttribute
{
    public override Task Satisfy()
    {
        IVoiceChannel? botsVc = Context.GuildConfig.BotsVoiceChannel;
        IVoiceChannel? usersVc = Context.User.VoiceChannel;

        if (botsVc != null && usersVc != null)
        {
            if (botsVc.Id != usersVc.Id)
            {
                // Console.WriteLine("Invalid");
                throw new PreconditionNotSatisfiedException(Responses.WrongVoiceChannel(botsVc));
            }
        }

        if (botsVc != null && usersVc == null)
        {
            // Console.WriteLine("Invalid");
            throw new PreconditionNotSatisfiedException(Responses.WrongVoiceChannel(botsVc));
        }
        return Task.CompletedTask;
    }
}