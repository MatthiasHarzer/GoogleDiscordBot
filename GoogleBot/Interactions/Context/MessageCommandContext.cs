using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions.Commands;
using GoogleBot.Services;

namespace GoogleBot.Interactions.Context;

/// <summary>
/// A context for modules using message commands
/// </summary>
public class MessageCommandContext : ICommandContext
{
    public IGuildUser User { get; }
    public ISocketMessageChannel TextChannel { get; }
    public IVoiceChannel? VoiceChannel { get; }
    public SocketGuild Guild { get; }
    public GuildConfig GuildConfig { get; }
    public Store DataStore => GuildConfig.DataStore;
    public CommandInfo CommandInfo { get; }
    public SocketInteraction Respondable => Command;

    public object?[] Arguments { get; }

    public object?[] UsedArguments => Arguments; //* There are no optional arguments on message commands

    /// <summary>
    /// The raw <see cref="SocketMessageCommand"/> from discord
    /// </summary>
    public SocketMessageCommand Command { get; }

    /// <summary>
    /// The message the command was used for
    /// </summary>
    public SocketMessage Message => Command.Data.Message;

    public MessageCommandContext(SocketMessageCommand command)
    {
        IGuildUser guildUser = (command.User as IGuildUser)!;

        TextChannel = command.Channel;

        Command = command;

        CommandInfo = InteractionMaster.GetMessageCommandFromName(command.CommandName)!;

        Guild = (SocketGuild?)guildUser.Guild!;

        User = guildUser;

        GuildConfig = GuildConfig.Get(guildUser.GuildId);

        VoiceChannel = guildUser.VoiceChannel;
        Arguments = new object?[] { command.Data.Message }; //Message commands have only one argument (the msg)
    }
}