using System;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using Discord;

namespace GoogleBot.Interactions.Commands;

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
    
    public object? DefaultValue { get; init; }


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