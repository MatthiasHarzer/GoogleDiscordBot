#nullable enable
using System;
using Discord;

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
[AttributeUsage(AttributeTargets.Parameter)]
public class MultipleAttribute : Attribute
{
    public bool IsMultiple { get; } = false;

    public MultipleAttribute()
    {
        IsMultiple = true;
    }

    public MultipleAttribute(bool isMultiple)
    {
        IsMultiple = isMultiple;
    }
}

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
    public bool IsDevOnly { get; } = false;

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
/// When applied, commands need the majority of a VC to execute it
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PreconditionAttribute : Attribute
{
    public bool RequiresMajority { get; }

    public string ButtonText { get; }

    public bool RequiresBotConnected { get; }


    /// <summary>
    /// Initializes the precondition attribute
    /// </summary>
    /// <param name="requiresMajority">Whether the majority of a VC is required to execute the command</param>
    /// <param name="majorityVoteButtonText">The text to put on the vote button</param>
    /// <param name="requiresBotConnected">Whether the bot must be in the a and the same VC as the player who executed the command</param>
    public PreconditionAttribute(bool requiresMajority = false, string majorityVoteButtonText = "Yes",
        bool requiresBotConnected = false)
    {
        RequiresMajority = requiresMajority;
        ButtonText = majorityVoteButtonText;
        RequiresBotConnected = requiresBotConnected;
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

/// <summary>
/// Defines the module as message commands
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class MessageCommandsModuleAttribute : Attribute
{
    public bool IsMessageCommandsModule { get; } = true;
}