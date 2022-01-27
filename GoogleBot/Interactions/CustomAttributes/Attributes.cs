﻿#nullable enable
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
public class RequiresMajorityAttribute : Attribute
{
    public bool RequiresMajority { get; } = false;

    public string ButtonText { get; } = "Yes";

    /// <summary>
    /// Default set RequiredMajority to true
    /// </summary>
    public RequiresMajorityAttribute()
    {
        RequiresMajority = true;
    }

    /// <summary>
    /// Explicitly set the RequiredMajority
    /// </summary>
    /// <param name="requiresMajority">The RequiredMajority</param>
    public RequiresMajorityAttribute(bool requiresMajority)
    {
        RequiresMajority = requiresMajority;
    }

    /// <summary>
    /// Sets the buttons text when voting + default true
    /// </summary>
    /// <param name="buttonText">The vote-buttons text</param>
    public RequiresMajorityAttribute(string buttonText)
    {
        RequiresMajority = true;
        ButtonText = buttonText;
    }

    public RequiresMajorityAttribute(bool requiresMajority, string buttonText)
    {
        RequiresMajority = requiresMajority;
        ButtonText = buttonText;
    }
}