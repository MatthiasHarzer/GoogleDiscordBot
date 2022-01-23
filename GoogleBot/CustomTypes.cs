﻿#nullable enable
using System;
using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions;
using static GoogleBot.Util;

namespace GoogleBot;

/// <summary>
/// Keeps context of currently executed command, such as the guild, user, channel etc..
/// </summary>
public class ExecuteContext
{
    public ExecuteContext(CommandInfo command, SocketCommandContext socketCommandContext)
    {
        IGuildUser? guildUser = socketCommandContext.User as IGuildUser;
        Command = command;
        Channel = socketCommandContext.Channel;
        VoiceChannel = guildUser?.VoiceChannel;
        Guild = socketCommandContext.Guild;
        User = socketCommandContext.User;
        GuildConfig = GuildConfig.Get(socketCommandContext.Guild.Id);
        IsSlashExecuted = false;
    }

    public ExecuteContext(SocketSlashCommand socketSlashCommand)
    {
        IGuildUser? guildUser = socketSlashCommand.User as IGuildUser;
        Command = CommandMaster.GetLegacyCommandFromName(socketSlashCommand.CommandName);
        Channel = socketSlashCommand.Channel;
        VoiceChannel = guildUser?.VoiceChannel;
        Guild = (SocketGuild?)guildUser?.Guild;
        User = socketSlashCommand.User;
        GuildConfig = GuildConfig.Get(guildUser?.Guild?.Id);
        IsSlashExecuted = true;
    }
    
    public ISocketMessageChannel Channel { get; set; }
    public CommandInfo Command { get; set; }

    public SocketGuild? Guild { get; set; }

    public SocketUser User { get; set; }
    
    public SocketMessageComponent Component { get; set; }
    
    public GuildConfig GuildConfig { get; }
    
    public IVoiceChannel? VoiceChannel { get; set; }

    public bool IsSlashExecuted { get; set; } = false;
    
}



/// <summary>
/// Simple return value for converting a message into its parts (command, args)
/// </summary>
public class CommandConversionInfo
{
    public CommandInfo Command { get; set; } = null!;
    public object[] Arguments { get; set; } = null!;
    public CommandConversionState State { get; set; }

    public (string, Type)[] TargetTypeParam { get; set; } = null!;


    public ParameterInfo[] MissingArgs { get; set; } = null!;
}


/// <summary>
/// Describes a parameter of a command
/// </summary>
public class ParameterInfo
{

    public string Name { get; init; } = null!;
    public string Summary { get; init; } = null!;
    public Type? Type { get; init; }
    public bool IsMultiple { get; init; }
    public bool IsOptional { get; init; }
    
    

    public override string ToString()
    {
        return $"{Name} - {Summary}\n Multiple: {IsMultiple}, Optional: {IsOptional}, Type: {Type}";
    }
}

/// <summary>
/// Describes a command
/// </summary>
public class CommandInfo
{
    public bool IsPrivate { get; set; } = false;
    public bool IsSlashOnly { get; init; } = false;
    public string? Name { get; init; }
    public string[] Aliases { get; init; } = {};
    public string? Summary { get; init; }
    public ParameterInfo[] Parameters { get; init; } = { };

    public MethodInfo? Method { get; init; }
}


/// <summary>
/// Return value of executed command. Can be embed or string.
/// </summary>
public class CommandReturnValue : IDisposable
{
    public EmbedBuilder? Embed { get; set; } = null;
    
    public ComponentBuilder? Components { get; set; }
    public string? Message { get; set; } = null;

    public CommandReturnValue()
    {
        
    }

    /// <summary>
    /// Configures a new CommandReturnValue with the given embed
    /// </summary>
    /// <param name="embed">The embed to return (that should be displayed)</param>
    public CommandReturnValue(EmbedBuilder embed)
    {
        WithEmbed(embed);
    }
    
    /// <summary>
    /// Configures a new CommandReturnValue with the given message
    /// </summary>
    /// <param name="message">The string message to return (that should be displayed)</param>
    public CommandReturnValue (string message)
    {
        WithText(message);
    }

    public CommandReturnValue WithComponents(ComponentBuilder components)
    {
        Components = components;
        return this;
    }

    public CommandReturnValue WithText(string message)
    {
        Message = message;
        return this;
    }

    public CommandReturnValue WithEmbed(EmbedBuilder embed)
    {
        embed.WithColor(RandomColor()); // Add a nice color to the embed
        Embed = embed;
        return this;
    }


    public void Dispose()
    {
        Components = null;
        Message = null;
        Embed = null;
    }
}


public class FormattedMessage
{
    public EmbedBuilder? Embed { get; set; } = null;

    public ComponentBuilder? Components { get; set; } = null;
    public string? Message { get; set; } = null;

    public FormattedMessage()
    {
        
    }

    /// <summary>
    /// Configures a new CommandReturnValue with the given embed
    /// </summary>
    /// <param name="embed">The embed to return (that should be displayed)</param>
    public FormattedMessage(EmbedBuilder embed)
    {
        WithEmbed(embed);
    }
    
    /// <summary>
    /// Configures a new CommandReturnValue with the given message
    /// </summary>
    /// <param name="message">The string message to return (that should be displayed)</param>
    public FormattedMessage (string message)
    {
        WithText(message);
    }

    public FormattedMessage WithComponents(ComponentBuilder components)
    {
        Components = components;
        return this;
    }

    public FormattedMessage WithText(string message)
    {
        Message = message;
        return this;
    }

    public FormattedMessage WithEmbed(EmbedBuilder embed)
    {
        embed.WithColor(RandomColor()); // Add a nice color to the embed
        Embed = embed;
        return this;
    }
}