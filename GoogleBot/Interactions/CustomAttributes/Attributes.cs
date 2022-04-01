#nullable enable
using System;
using System.Linq;
using System.Net;
using Discord;
using GoogleBot.Interactions.Commands;

namespace GoogleBot.Interactions.CustomAttributes;

/// <summary>
/// Defines, whether the commands reply should be private or public
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PrivateAttribute : Attribute
{
    /// <summary>
    /// Gets, whether the commands reply should be private or public, if set
    /// </summary>
    public bool IsPrivate { get; }

    public PrivateAttribute()
    {
        IsPrivate = true;
    }

    public PrivateAttribute(bool isPrivate)
    {
        IsPrivate = isPrivate;
    }
}

/// <summary>
/// Links the method to a componentInteraction. If not id is provided, all interactions will be linked
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class LinkComponentInteractionAttribute : Attribute
{
    /// <summary>
    /// The custom id of the interaction
    /// </summary>
    public string CustomId { get; }

    /// <summary>
    /// Sets the CustomId to * which stands for any id
    /// </summary>
    public LinkComponentInteractionAttribute()
    {
        CustomId = "*"; // = all ids
    }

    /// <summary>
    /// Links the method only to interactions with the given id
    /// </summary>
    /// <param name="customId">The custom id to listen to</param>
    public LinkComponentInteractionAttribute(string customId)
    {
        CustomId = customId;
    }
}

/// <summary>
/// Defines if the parameter can have multiple words (with whitespaces)
/// </summary>
// [AttributeUsage(AttributeTargets.Parameter)]
// public class MultipleAttribute : Attribute
// {
//     public bool IsMultiple { get; }
//
//     public MultipleAttribute()
//     {
//         IsMultiple = true;
//     }
//
//     public MultipleAttribute(bool isMultiple)
//     {
//         IsMultiple = isMultiple;
//     }
// }

/// <summary>
/// Sets the applicationOptionType of a parameter 
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class OptionTypeAttribute : Attribute
{
    public ApplicationCommandOptionType Type { get; }

    public OptionTypeAttribute(ApplicationCommandOptionType t)
    {
        Type = t;
    }
}

/// <summary>
/// Overrides an default defer on command execusion to implement own 
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OverrideDeferAttribute : Attribute
{
    public bool DeferOverride { get; } = false;

    public OverrideDeferAttribute()
    {
        DeferOverride = true;
    }

    public OverrideDeferAttribute(bool deferOverride)
    {
        DeferOverride = deferOverride;
    }
}

/// <summary>
/// commands / modules will only be added as guild commands in given dev-guild
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class DevOnlyAttribute : Attribute
{
    public bool IsDevOnly { get; }

    public DevOnlyAttribute()
    {
        IsDevOnly = true;
    }

    public DevOnlyAttribute(bool isDevOnly)
    {
        IsDevOnly = isDevOnly;
    }
}



/// <summary>
/// When applied, the command will get an optional ephemeral param, making the message ephemeral
/// </summary>
public class OptionalEphemeralAttribute : Attribute
{
    public bool IsOptionalEphemeral { get; init; }

    public OptionalEphemeralAttribute(bool isOptionalEphemeral = true)
    {
        IsOptionalEphemeral = isOptionalEphemeral;
    }

    public static bool Default => new OptionalEphemeralAttribute().IsOptionalEphemeral;
}

[AttributeUsage(AttributeTargets.Method)]
public class AutoDeleteOldComponentsAttribute : Attribute
{
    public bool AutoDelete { get; }

    public AutoDeleteOldComponentsAttribute(bool autoDelete = true)
    {
        AutoDelete = autoDelete;
    }
}


[AttributeUsage(AttributeTargets.Method)]
public class VoteConfigAttribute : Attribute
{
    public bool Enabled { get; }
    public string ButtonText { get; }

    public VoteConfigAttribute(bool enabled = true, string buttonText = "Vote")
    {
        Enabled = enabled;
        ButtonText = buttonText;
    }
}

