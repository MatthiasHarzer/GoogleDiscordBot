#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
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
    public bool IsEphemeral { get; }

    public PrivateAttribute(bool isEphemeral)
    {
        IsEphemeral = isEphemeral;
    }
}

/// <summary>
/// Defines, whether the command can only be used as a slash command
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SlashOnlyCommandAttribute : Attribute
{
    /// <summary>
    /// If true, the command is only accessible as a slash command
    /// </summary>
    public bool IsSlashOnlyCommand { get; }

    public SlashOnlyCommandAttribute(bool isSlashOnlyCommand)
    {
        IsSlashOnlyCommand = isSlashOnlyCommand;
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
    public string CustomId { get;} 
    
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