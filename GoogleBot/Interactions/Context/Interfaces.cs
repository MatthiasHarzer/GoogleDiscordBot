﻿using Discord;
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
    public IGuildUser User { get; }

    /// <summary>
    /// The Text Channel where the interaction was triggerd
    /// </summary>
    public ISocketMessageChannel TextChannel { get; }

    /// <summary>
    /// The users voice channel, if the user is connected to one
    /// </summary>
    public IVoiceChannel? VoiceChannel { get; }

    /// <summary>
    /// The command info, if available
    /// </summary>
    public CommandInfo? CommandInfo { get; }

    /// <summary>
    /// The guild where the interaction takes place
    /// </summary>
    public SocketGuild Guild { get; }
    

    /// <summary>
    /// The <see cref="Services.GuildConfig"/> of the <see cref="Guild"/>
    /// </summary>
    public GuildConfig GuildConfig { get; }

    public SocketInteraction Respondable { get; }

    /// <summary>
    /// The bots client
    /// </summary>
    public DiscordSocketClient Client => Globals.Client;
}

/// <summary>
/// The context of every command-based interaction with the bot
/// <seealso cref="GoogleBot.Interactions.Modules.SlashCommandModuleBase"/>
/// <seealso cref="GoogleBot.Interactions.Modules.MessageCommandModuleBase"/>
/// </summary>
public interface ICommandContext : IContext
{
    // /// <summary>
    // /// The <see cref="Commands.CommandInfo"/> of interaction
    // /// </summary>
    public new CommandInfo CommandInfo { get; }

    /// <summary>
    /// Whether the command should be ephemeral or not
    /// </summary>
    public bool IsEphemeral => false;

    /// <summary>
    /// The arguments for the executed command (including default values for optional args)
    /// </summary>
    public object?[] Arguments { get; }

    /// <summary>
    /// Includes only the arguments the user used (no defaults)
    /// </summary>
    public object?[] UsedArguments { get; }
}