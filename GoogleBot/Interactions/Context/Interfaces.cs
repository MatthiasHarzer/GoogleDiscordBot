using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions.Commands;
using GoogleBot.Services;

namespace GoogleBot.Interactions.Context;

/// <summary>
/// The base context of every interaction with the bot
/// </summary>
public interface IContext
{
    /// <summary>
    /// The user who triggered the interaction
    /// </summary>
    public SocketUser User { get; }

    /// <summary>
    /// The Text Channel where the interaction was triggerd
    /// </summary>
    public ISocketMessageChannel TextChannel { get; }

    /// <summary>
    /// The users voice channel, if the user is connected to one
    /// </summary>
    public IVoiceChannel? VoiceChannel { get; }

    /// <summary>
    /// The guild where the interaction takes place
    /// </summary>
    public SocketGuild Guild { get; }

    /// <summary>
    /// The <see cref="Services.GuildConfig"/> of the <see cref="Guild"/>
    /// </summary>
    public GuildConfig GuildConfig { get; }

    public IDiscordInteraction Respondable { get; }
}

/// <summary>
/// The context of every command-based interaction with the bot
/// <seealso cref="GoogleBot.Interactions.Modules.SlashCommandModuleBase"/>
/// <seealso cref="GoogleBot.Interactions.Modules.MessageCommandModuleBase"/>
/// </summary>
public interface ICommandContext : IContext
{
    /// <summary>
    /// The <see cref="Commands.CommandInfo"/> of interaction
    /// </summary>
    public CommandInfo CommandInfo { get; }

    /// <summary>
    /// Whether the command should be ephemeral or not
    /// </summary>
    public bool IsEphemeral => false;
}