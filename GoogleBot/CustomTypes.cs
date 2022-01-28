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
    public Preconditions Preconditions { get; init; } = new Preconditions();

    public bool OverrideDefer { get; init; } = false;


    public ParameterInfo[] Parameters { get; init; } = Array.Empty<ParameterInfo>();

    public MethodInfo? Method { get; init; }

    public override string ToString()
    {
        return
            $"{Name} {string.Join(" ", Parameters.ToList().ConvertAll(p => p.IsOptional ? $"[<{p.Name}>]" : $"<{p.Name}>"))} : {Summary}";
    }

    public JsonObject ToJson()
    {
        JsonObject json = new JsonObject
        {
            { "name", Name },
            { "summery", Summary },
            { "private", IsPrivate },
            { "overrideDefer", OverrideDefer },
            { "devonly", IsDevOnly },
            { "optionalEphemeral", IsOptionalEphemeral },
            { "parameters", new JsonArray(Parameters.ToList().ConvertAll(p => (JsonNode)p.ToJson()).ToArray()) }
        };

        return json;
    }

    public CommandInfo FromJson(JsonObject jsonObject)
    {
        string name = null!, summery = null!;
        bool isPrivate = false, overrideDefer = false, devonly = false, optionalEphemeral = false;
        JsonArray parameters = new JsonArray();

        if (jsonObject.TryGetPropertyValue("name", out var n))
        {
            name = n?.ToString() ?? throw new InvalidOperationException();
        }

        if (jsonObject.TryGetPropertyValue("summery", out var s))
        {
            summery = s?.ToString() ?? "";
        }

        if (jsonObject.TryGetPropertyValue("private", out var ip))
        {
            isPrivate = ip?.GetValue<bool>() ?? false;
        }

        if (jsonObject.TryGetPropertyValue("private", out var d))
        {
            overrideDefer = d?.GetValue<bool>() ?? false;
        }

        if (jsonObject.TryGetPropertyValue("devonly", out var dev))
        {
            devonly = dev?.GetValue<bool>() ?? false;
        }

        if (jsonObject.TryGetPropertyValue("optionalEphemeral", out var oe))
        {
            optionalEphemeral = oe?.GetValue<bool>() ?? false;
        }

        if (jsonObject.TryGetPropertyValue("parameters", out var pa))
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
    public EmbedBuilder? Embed { get; set; } = null;

    /// <summary>
    /// Gets the built embed
    /// </summary>
    public Embed BuiltEmbed => Embed?.Build()!;


    public MessageComponent BuiltComponents => Components?.Build()!;

    /// <summary>
    /// The components of the message
    /// </summary>
    public ComponentBuilder? Components { get; set; } = null;

    /// <summary>
    /// The text of the message
    /// </summary>
    public string? Message { get; set; } = null;

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

/// <summary>
/// A context to execute a modules methods (command, interaction) in
/// May include CommandInfo, Guild, User, Channel...
/// </summary>
public class Context
{
    public Context()
    {
        GuildConfig = GuildConfig.Get(null);
        User = null!;
    }

    public Context(SocketMessageComponent component)
    {
        IGuildUser? guildUser = component.User as IGuildUser;
        GuildConfig = GuildConfig.Get(guildUser?.GuildId);
        User = component.User;
        Channel = component.Channel;
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

    /// <summary>
    /// The original command 
    /// </summary>
    public SocketSlashCommand? Command { get; }

    /// <summary>
    /// The text channel 
    /// </summary>
    public ISocketMessageChannel? Channel { get; }

    /// <summary>
    /// The CommandInfo from a given command
    /// </summary>
    public CommandInfo? CommandInfo { get; }

    /// <summary>
    /// Whether the command should be ephemeral or not
    /// </summary>
    public bool IsEphemeral { get; } = false;

    /// <summary>
    /// The arguments for the executed command (including default values for optional args)
    /// </summary>
    public object[] Arguments { get; } = Array.Empty<object>();

    /// <summary>
    /// The arguments actually used by the user (filter out optional args)
    /// </summary>
    public object[] UsedArgs
    {
        get
        {
            try
            {
                //* Get the used args (remove default values)
                List<object> usedArgs = new();
                // int i = 0;
                for (int i = 0; i < Arguments.Length; i++)
                {
                    // Console.WriteLine(args[i] + " != " + CommandInfo.Method!.GetParameters()[i].DefaultValue + " : " +
                    //                   (args[i] != CommandInfo.Method!.GetParameters()[i].DefaultValue));
                    if (!CommandInfo!.Method!.GetParameters()[i].HasDefaultValue || Arguments[i].ToString() !=
                        CommandInfo.Method!.GetParameters()[i].DefaultValue!.ToString())
                    {
                        usedArgs.Add(Arguments[i]);
                    }
                }

                return usedArgs.ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return Array.Empty<object>();
            }
        }
    }

    /// <summary>
    /// The Guild
    /// </summary>
    public SocketGuild? Guild { get; }

    /// <summary>
    /// The user who executed/triggered the interaction
    /// </summary>
    public SocketUser User { get; }

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
    public IVoiceChannel? VoiceChannel { get; }
}