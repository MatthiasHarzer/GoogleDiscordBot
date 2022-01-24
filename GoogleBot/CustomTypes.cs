﻿#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions;
using static GoogleBot.Util;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace GoogleBot;


/// <summary>
/// Simple return value for converting a message into its parts (command, args)
/// </summary>
public class CommandConversionInfo
{
    public CommandInfo Command { get; init; } = null!;
    public object[] Arguments { get; init; } = Array.Empty<object>();
    public CommandConversionState State { get; init; }

    public (string, Type)[] TargetTypeParam { get; init; } = Array.Empty<(string, Type)>();


    public ParameterInfo[] MissingArgs { get; init; } = global::System.Array.Empty<global::GoogleBot.ParameterInfo>();
}


/// <summary>
/// Describes a parameter of a command
/// </summary>
public class ParameterInfo
{

    public string Name { get; init; } = null!;
    public string Summary { get; init; } = null!;
    public ApplicationCommandOptionType Type { get; init; }
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
    public CommandInfo(){}

    public CommandInfo(SocketApplicationCommand command)
    {
        Name = command.Name;
        Summary = command.Description;
        Parameters = command.Options.ToList().ConvertAll(o => new ParameterInfo
        {
            Name = o.Name,
            Summary = o.Description,
            Type = o.Type,
            IsOptional = o is { IsRequired: false }
        }).ToArray();
    }
    public bool IsPrivate { get; set; } = false;
    public string Name { get; init; } = string.Empty;
    public string Summary { get; init; } = "No description available";
    public ParameterInfo[] Parameters { get; init; } = global::System.Array.Empty<global::GoogleBot.ParameterInfo>();

    public MethodInfo? Method { get; init; }

    public override string ToString()
    {
        return
            $"{Name} {string.Join(" ", Parameters.ToList().ConvertAll(p => p.IsOptional ? $"[<{p.Name}>]" : $"<{p.Name}>"))} : {Summary}";
    }
    
}


/// <summary>
/// A message to send/reply of type text or embed with additional optional Components.
/// Used to unify the bots messages
/// </summary>
public class FormattedMessage
{
    /// <summary>
    /// The embed of the message
    /// </summary>
    public EmbedBuilder? Embed { get; set; } = null;

    /// <summary>
    /// The components of the message
    /// </summary>
    public ComponentBuilder? Components { get; set; } = null;
    
    /// <summary>
    /// The text of the message
    /// </summary>
    public string? Message { get; set; } = null;
    

    /// <summary>
    /// Configures a new FormattedMessage with the given embed
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

    /// <summary>
    /// Adds components to the message
    /// </summary>
    /// <param name="components">The components to add</param>
    /// <returns>The instance with added components</returns>
    public FormattedMessage WithComponents(ComponentBuilder components)
    {
        Components = components;
        return this;
    }

    /// <summary>
    /// Adds a text to the message
    /// </summary>
    /// <param name="message">The text</param>
    /// <returns></returns>
    public FormattedMessage WithText(string message)
    {
        Message = message;
        return this;
    }

    /// <summary>
    /// Adds an embed to the message
    /// </summary>
    /// <param name="embed">The embed to add</param>
    /// <returns></returns>
    public FormattedMessage WithEmbed(EmbedBuilder embed)
    {
        embed.WithColor(RandomColor()); // Add a nice color to the embed
        embed.WithCurrentTimestamp(); //
        Embed = embed;
        return this;
    }
}


/// <summary>
/// A context to execute a modules methods (command, interaction) in
/// May include CommandInfo, Guild, User, Channel...
/// </summary>
public class Context
{
    public Context()
    {
        GuildConfig = GuildConfig.Get(null);
    }
    
    
    /// <summary>
    /// Creates a new instance from a <see cref="SocketSlashCommand"/>
    /// </summary>
    /// <param name="command">The SocketSlashCommand</param>
    public Context(SocketSlashCommand command)
    {
       
        IGuildUser? guildUser = command.User as IGuildUser;
        Channel = command.Channel;
        CommandInfo = CommandMaster.GetCommandFromName(command.CommandName);
        Command = command;
        Guild = (SocketGuild?)guildUser?.Guild;
        User = command.User;
        GuildConfig = GuildConfig.Get(guildUser?.GuildId);
        VoiceChannel = guildUser?.VoiceChannel;
    }
    
    /// <summary>
    /// The original command 
    /// </summary>
    public SocketSlashCommand? Command {get;}
    
    
    /// <summary>
    /// The text channel 
    /// </summary>
    public ISocketMessageChannel? Channel { get; }
    
    /// <summary>
    /// The CommandInfo from a given command
    /// </summary>
    public CommandInfo? CommandInfo { get;  }
    
    /// <summary>
    /// The Guild
    /// </summary>
    public SocketGuild? Guild { get;  }
    
    /// <summary>
    /// The user who executed/triggered the interaction
    /// </summary>
    public SocketUser? User { get;  }
    
    /// <summary>
    /// ?
    /// </summary>
    public SocketMessageComponent? Component { get; set; }
    
    /// <summary>
    /// The GuildConfig for the guild with an AudioPlayer
    /// </summary>
    public GuildConfig GuildConfig { get; }
    
    /// <summary>
    /// The voice channel of the user
    /// </summary>
    public IVoiceChannel? VoiceChannel { get;  }

}