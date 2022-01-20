#nullable enable
using System;
using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static GoogleBot.Util;

namespace GoogleBot.Interactions;

/// <summary>
/// Keeps context of currently executed command, such as the guild, user, channel etc..
/// </summary>
public class ExecuteContext
{
    public ExecuteContext(CommandInfo? command, SocketCommandContext socketCommandContext)
    {
        IGuildUser? guildUser = socketCommandContext.User as IGuildUser;
        Command = command;
        Channel = socketCommandContext.Channel;
        VoiceChannel = guildUser?.VoiceChannel;
        Guild = socketCommandContext.Guild;
        User = socketCommandContext.User;
        GuildConfig = GuildConfig.Get(socketCommandContext.Guild.Id);
    }

    public ExecuteContext(SocketSlashCommand socketSlashCommand)
    {
        IGuildUser? guildUser = socketSlashCommand.User as IGuildUser;
        Command = CommandMaster.GetCommandFromName(socketSlashCommand.CommandName);
        Channel = socketSlashCommand.Channel;
        VoiceChannel = guildUser?.VoiceChannel;
        Guild = (SocketGuild?)guildUser?.Guild;
        User = socketSlashCommand.User;
        GuildConfig = GuildConfig.Get(guildUser?.Guild?.Id);
    }
    
    public ISocketMessageChannel Channel { get; set; }
    public CommandInfo? Command { get; set; }

    public SocketGuild? Guild { get; set; }

    public SocketUser User { get; set; }
    
    public GuildConfig GuildConfig { get; }
    
    public IVoiceChannel? VoiceChannel { get; set; }
    
    

    public static (ExecuteContext, CommandConversionInfo) From(SocketCommandContext socketCommandContext)
    {
        CommandConversionInfo conversionInfo = GetCommandInfoFromMessage(socketCommandContext.Message);
        Console.WriteLine("Conversion State: " + conversionInfo.State + " (" + conversionInfo?.Command?.Name + ")");
        return (new ExecuteContext(conversionInfo?.Command, socketCommandContext), conversionInfo)!;
    }
}

/// <summary>
/// Simple return value for converting a message into its parts (command, args)
/// </summary>
public class CommandConversionInfo
{
    public CommandInfo Command { get; set; }
    public object[] Arguments { get; set; }
    public CommandConversionState State { get; set; }

    public (string, Type)[] TargetTypeParam { get; set; }


    public ParameterInfo[] MissingArgs { get; set; }
}


/// <summary>
/// Describes a parameter of a command
/// </summary>
public class ParameterInfo
{
    public string Name { get; init; }
    public string Summary { get; init; }
    public Type Type { get; init; }
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
    public bool IsSlashOnlyCommand { get; init; } = false;
    public string Name { get;  init;}
    public string[] Aliases { get; init; }
    public string Summary { get; init; }
    public ParameterInfo[] Parameters { get; init; }
    
    public MethodInfo Method { get; init; }
}


/// <summary>
/// Return value of executed command. Can be embed or string.
/// </summary>
public class CommandReturnValue
{
    public EmbedBuilder? Embed { get; } = null;
    public string? Message { get; } = null;

    /// <summary>
    /// Configures a new CommandReturnValue with the given embed
    /// </summary>
    /// <param name="embed">The embed to return (that should be displayed)</param>
    public CommandReturnValue(EmbedBuilder embed)
    {
        embed.WithColor(RandomColor()); // Add a nice color to the embed
        Embed = embed;
    }

    
    /// <summary>
    /// Configures a new CommandReturnValue with the given message
    /// </summary>
    /// <param name="message">The string message to return (that should be displayed)</param>
    public CommandReturnValue (string message)
    {
        Message = message;
    }
}

