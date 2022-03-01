using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using GoogleBot.Interactions.Context;
using GoogleBot.Interactions.Modules;
using ModuleBase = Discord.Commands.ModuleBase;

namespace GoogleBot.Interactions.Commands;

public enum CommandType
{
    SlashCommand = 0,
    MessageCommand = 1,
    UserCommand = 2, //* Not implemented yet
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
    public bool IsValid => Method != null;


    public ParameterInfo[] Parameters { get; init; } = Array.Empty<ParameterInfo>();

    public MethodInfo? Method { get; init; }

    /// <summary>
    /// Creates a new module instance for the command with a context
    /// </summary>
    /// <param name="context">The commands <see cref="SlashCommandContext"/></param>
    /// <returns>A new instance of the module</returns>
    public SlashCommandModuleBase GetNewModuleInstanceWith(SlashCommandContext context)
    {
        SlashCommandModuleBase module = (SlashCommandModuleBase)Activator.CreateInstance(Module)!;
        module.SetContext(context);
        return module;
    }
    
    /// <summary>
    /// Creates a new module instance for the command with a context
    /// </summary>
    /// <param name="context">The commands <see cref="SlashCommandContext"/></param>
    /// <returns>A new instance of the module</returns>
    public MessageCommandModuleBase GetNewModuleInstanceWith(MessageCommandContext context)
    {
        MessageCommandModuleBase module = (MessageCommandModuleBase)Activator.CreateInstance(Module)!;
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
                    { "id", Id },
                    { "name", Name },
                    { "type", (int)Type },
                };
            case CommandType.SlashCommand:
            default:
                return new JsonObject
                {
                    { "id", Id },
                    { "name", Name },
                    { "summery", Summary },
                    { "type", (int)Type },
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
        bool isPrivate = defaults.IsPrivate,
            overrideDefer = defaults.OverrideDefer,
            devonly = defaults.IsDevOnly,
            optionalEphemeral = defaults.IsOptionalEphemeral;
        CommandType type = defaults.Type;
        JsonArray parameters = new JsonArray();

        if (jsonObject.TryGetPropertyValue("type", out JsonNode? t))
        {
            type = (CommandType)Math.Min((t?.GetValue<int>() ?? 0), Enum.GetNames(typeof(CommandType)).Length - 1);
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