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
using GoogleBot.Interactions.Context;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace GoogleBot;

public enum CommandType
{
    SlashCommand = 0,
    MessageCommand = 1,
    UserCommand = 2, //* Not implemented yet
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





