#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace GoogleBot;

public enum CommandType
{
    SlashCommand = 0,
    MessageCommand = 1,
    UserCommand = 2, //* Not implemented yet
}

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
    /// The <see cref="GoogleBot.GuildConfig"/> of the <see cref="Guild"/>
    /// </summary>
    public GuildConfig GuildConfig { get; }
    
    public IDiscordInteraction Respondable { get; }
    
}

public interface ICommandContext : IContext
{
    /// <summary>
    /// The <see cref="GoogleBot.CommandInfo"/> of interaction
    /// </summary>
    public CommandInfo CommandInfo { get; }
    
    

    public bool IsEphemeral => false;
}

internal interface IJsonSerializable<out T>
{
    JsonObject ToJson();
    T FromJson(JsonObject jsonObject);
}

/// <summary>
/// Preconditions for a command
/// </summary>
public class Preconditions
{
    public bool RequiresMajority { get; init; }

    public string MajorityVoteButtonText { get; init; } = string.Empty;

    public bool RequiresBotConnected { get; init; }
}

/// <summary>
/// Describes a parameter of a command
/// </summary>
public class ParameterInfo : IJsonSerializable<ParameterInfo>
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

    public JsonObject ToJson()
    {
        return new JsonObject
        {
            { "name", Name },
            { "summery", Summary },
            { "multiple", IsMultiple },
            { "optional", IsOptional },
            { "type", Util.OptionTypeToString(Type) }
        };
    }

    public ParameterInfo FromJson(JsonObject jsonObject)
    {
        string name = null!, summery = null!;
        bool isMultiple = false, isOptional = false;
        ApplicationCommandOptionType type = (ApplicationCommandOptionType)0;

        if (jsonObject.TryGetPropertyValue("name", out var n))
        {
            name = n?.ToString() ?? throw new InvalidOperationException();
        }

        if (jsonObject.TryGetPropertyValue("summery", out var s))
        {
            summery = s?.ToString() ?? "";
        }

        if (jsonObject.TryGetPropertyValue("multiple", out var m))
        {
            isMultiple = m?.GetValue<bool>() ?? false;
        }

        if (jsonObject.TryGetPropertyValue("optional", out var o))
        {
            isOptional = o?.GetValue<bool>() ?? false;
        }

        if (jsonObject.TryGetPropertyValue("type", out var t))
        {
            type = Util.ToOptionType(t?.ToString());
        }

        if (name == null || summery == null)
        {
            throw new SerializationException("Name or summery invalid");
        }

        return new ParameterInfo
        {
            Name = name,
            Summary = summery,
            IsMultiple = isMultiple,
            IsOptional = isOptional,
            Type = type,
        };
    }
}

/// <summary>
/// Describes a command
/// </summary>
public class CommandInfo : IJsonSerializable<CommandInfo>
{
    public bool IsPrivate { get; init; } = false;
    public string Name { get; init; } = string.Empty;
    public string Summary { get; init; } = "No description available";
    public bool IsDevOnly { get; init; } = false;

    public bool IsOptionalEphemeral { get; init; }
    public Preconditions Preconditions { get; init; } = new();

    public Type Module => Method?.DeclaringType!;

    public bool OverrideDefer { get; init; } = false;

    public CommandType Type;

    /// <summary>
    /// A unique identifier for the command. There can't be two commands with the same name and type.
    /// </summary>
    public string Id => $"{Name}-{Type}";

    /// <summary>
    /// Check if the command can be executed as is
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (Method == null)
                return false;
            return true;
        }
    }
    

    public ParameterInfo[] Parameters { get; init; } = Array.Empty<ParameterInfo>();

    public MethodInfo? Method { get; init; }

    /// <summary>
    /// Creates a new module instance for the command with a context
    /// </summary>
    /// <param name="context">The commands <see cref="SlashCommandContext"/>></param>
    /// <returns>A new instance of the module</returns>
    public CommandModuleBase GetNewModuleInstanceWith(ICommandContext context)
    {
        CommandModuleBase module = (CommandModuleBase)Activator.CreateInstance(Module)!;
        module.SetContext(context);
        return module;
    }

    public override string ToString()
    {
        return
            $"({Type}) {Name} {string.Join(" ", Parameters.ToList().ConvertAll(p => p.IsOptional ? $"[<{p.Name}>]" : $"<{p.Name}>"))}";
    }

    public JsonObject ToJson()
    {
        switch (Type)
        {
            case CommandType.MessageCommand:
            case CommandType.UserCommand:
                return new JsonObject
                {
                    { "id", Id},
                    { "name", Name },
                    { "type", (int)Type },
                };
            case CommandType.SlashCommand:
            default:
                return new JsonObject
                {
                    { "id", Id},
                    { "name", Name },
                    { "summery", Summary },
                    { "type", (int)Type},
                    { "private", IsPrivate },
                    { "overrideDefer", OverrideDefer },
                    { "devonly", IsDevOnly },
                    { "optionalEphemeral", IsOptionalEphemeral },
                    { "parameters", new JsonArray(Parameters.ToList().ConvertAll(p => (JsonNode)p.ToJson()).ToArray()) }
                };
        }
    }

    public CommandInfo FromJson(JsonObject jsonObject)
    {
        CommandInfo defaults = new CommandInfo();
        string name = defaults.Name, summery = defaults.Summary;
        bool isPrivate = defaults.IsPrivate, overrideDefer = defaults.OverrideDefer, devonly = defaults.IsDevOnly, optionalEphemeral = defaults.IsOptionalEphemeral;
        CommandType type = defaults.Type;
        JsonArray parameters = new JsonArray();

        if (jsonObject.TryGetPropertyValue("type", out JsonNode? t))
        {
            type = (CommandType)Math.Min((t?.GetValue<int>() ?? 0), Enum.GetNames(typeof(CommandType)).Length-1);
        }

        if (jsonObject.TryGetPropertyValue("name", out JsonNode? n))
        {
            name = n?.ToString() ?? throw new InvalidOperationException();
        }

        if (jsonObject.TryGetPropertyValue("summery", out JsonNode? s))
        {
            summery = s?.ToString() ?? "";
        }

        if (jsonObject.TryGetPropertyValue("private", out JsonNode? ip))
        {
            isPrivate = ip?.GetValue<bool>() ?? isPrivate;
        }

        if (jsonObject.TryGetPropertyValue("private", out JsonNode? d))
        {
            overrideDefer = d?.GetValue<bool>() ?? overrideDefer;
        }

        if (jsonObject.TryGetPropertyValue("devonly", out JsonNode? dev))
        {
            devonly = dev?.GetValue<bool>() ?? devonly;
        }

        if (jsonObject.TryGetPropertyValue("optionalEphemeral", out JsonNode? oe))
        {
            optionalEphemeral = oe?.GetValue<bool>() ?? optionalEphemeral;
        }

        if (jsonObject.TryGetPropertyValue("parameters", out JsonNode? pa))
        {
            parameters = pa?.AsArray() ?? new JsonArray();
        }


        if (name == null || summery == null)
        {
            throw new SerializationException("Name or summery invalid");
        }

        return new CommandInfo
        {
            Name = name,
            Summary = summery,
            Type = type,
            IsPrivate = isPrivate,
            OverrideDefer = overrideDefer,
            IsDevOnly = devonly,
            IsOptionalEphemeral = optionalEphemeral,
            Parameters = parameters.ToList().OfType<JsonNode>().ToList()
                .ConvertAll(p => new ParameterInfo().FromJson((JsonObject)p)).ToArray(),
        };
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
    public EmbedBuilder? Embed { get; set; }

    /// <summary>
    /// Gets the built embed
    /// </summary>
    public Embed BuiltEmbed => Embed?.Build()!;


    public MessageComponent BuiltComponents => Components?.Build()!;

    /// <summary>
    /// The components of the message
    /// </summary>
    public ComponentBuilder? Components { get; set; }

    /// <summary>
    /// The text of the message
    /// </summary>
    public string? Message { get; set; }

    public FormattedMessage()
    {
    }

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
    public FormattedMessage(string message)
    {
        WithText(message);
    }

    public FormattedMessage FromExisting(FormattedMessage fm)
    {
        Message = fm?.Message ?? Message;
        Embed = fm?.Embed ?? Embed;
        Components = fm?.Components ?? Components;
        return this;
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
        embed.WithColor(Util.RandomColor()); // Add a nice color to the embed
        embed.WithCurrentTimestamp(); //
        Embed = embed;
        return this;
    }
}

public class SlashCommandContext : ICommandContext
{
    public SocketUser User { get; }
    public ISocketMessageChannel TextChannel { get; }
    public IVoiceChannel? VoiceChannel { get; }
    public GuildConfig GuildConfig { get; }
    public SocketGuild Guild { get; }
    
    /// <summary>
    /// The CommandInfo from a given command
    /// </summary>
    public CommandInfo CommandInfo { get; }

    public IDiscordInteraction Respondable => Command;

    /// <summary>
    /// Whether the command should be ephemeral or not
    /// </summary>
    public bool IsEphemeral { get; } = false;

    /// <summary>
    /// The arguments for the executed command (including default values for optional args)
    /// </summary>
    public object[] Arguments { get; }
    
    /// <summary>
    /// The original command 
    /// </summary>
    public SocketSlashCommand Command { get; }
    
    
    /// <summary>
    /// Creates a new <see cref="SlashCommandContext"/> from a <see cref="SocketSlashCommand"/>
    /// </summary>
    /// <param name="command"></param>
    /// <exception cref="ArgumentException"></exception>
    public SlashCommandContext(SocketSlashCommand command)
    {
        IGuildUser guildUser = (command.User as IGuildUser)!;
        
        TextChannel = command.Channel;
        
        CommandInfo = CommandMaster.GetCommandFromName(command.CommandName);
        Command = command;
        Guild = (SocketGuild?)guildUser.Guild!;
        User = command.User;
        GuildConfig = GuildConfig.Get(guildUser.GuildId);
        VoiceChannel = guildUser.VoiceChannel;

        object[] args = new object[CommandInfo.Method!.GetParameters().Length];

        object[] options = command.Data.Options.ToList().ConvertAll(option => option.Value).ToArray();


        int i;
        //* Fill the args with the provided option values
        for (i = 0; i < Math.Min(options.Length, args.Length); i++)
        {
            args[i] = options[i];
        }

        //* Fill remaining args with their default values
        for (; i < args.Length; i++)
        {
            if (!CommandInfo.Method.GetParameters()[i].HasDefaultValue)
            {
                throw new ArgumentException("Missing options.");
            }

            args[i] = CommandInfo.Method!.GetParameters()[i].DefaultValue!;
        }

        Arguments = args;

        if (CommandInfo.IsOptionalEphemeral)
        {
            int optionsHidden = Command.Data.Options.ToList()
                .FindAll(o => o.Name.ToLower() == "hidden" && (bool)o.Value).Count;
            if (optionsHidden > 1)
                throw new ArgumentException("Too many options for \"hidden\"");
            IsEphemeral = optionsHidden > 0;
        }
    }
}

public class MessageCommandContext : ICommandContext
{
    public SocketUser User { get; }
    public ISocketMessageChannel TextChannel { get; }
    public IVoiceChannel? VoiceChannel { get; }
    public SocketGuild Guild { get; }
    public GuildConfig GuildConfig { get; }
    public CommandInfo CommandInfo { get; }
    public IDiscordInteraction Respondable => Command;
    
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
        CommandInfo = CommandMaster.GetMessageCommandFromName(command.CommandName);
        Guild = (SocketGuild?)guildUser.Guild!;
        User = command.User;
        GuildConfig = GuildConfig.Get(guildUser.GuildId);
        VoiceChannel = guildUser.VoiceChannel;
    }
}

public class InteractionContext : IContext
{
    public SocketUser User { get; }
    public ISocketMessageChannel TextChannel { get; }
    public IVoiceChannel? VoiceChannel { get; }
    public SocketGuild Guild { get; }
    public GuildConfig GuildConfig { get; }

    public SocketMessageComponent Component { get; }
    
    public IDiscordInteraction Respondable => Component;
    
    public InteractionContext(SocketMessageComponent component)
    {
        IGuildUser guildUser = (component.User as IGuildUser)!;
        GuildConfig = GuildConfig.Get(guildUser.GuildId);
        User = component.User;
        TextChannel = component.Channel;
        VoiceChannel = guildUser.VoiceChannel;
        Guild = (SocketGuild)guildUser.Guild;
        Component = component;
    }
}