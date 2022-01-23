#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;

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

[AttributeUsage(AttributeTargets.Class)]
public class TextCommandModuleAttribute : Attribute
{
    public bool IsTextCommandModule { get; }

    public TextCommandModuleAttribute()
    {
        IsTextCommandModule = false;
    }
    public TextCommandModuleAttribute(bool isTextCommandModule)
    {
        IsTextCommandModule = isTextCommandModule;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class LinkComponentInteractionAttribute : Attribute
{
    public string CustomId { get;} 
    public LinkComponentInteractionAttribute()
    {
        CustomId = "*"; // = all ids
    }

    public LinkComponentInteractionAttribute(string customId)
    {
        CustomId = customId;
    }
}

[AttributeUsage(AttributeTargets.Parameter)]
public class MultipleAttribute : Attribute
{
    public bool IsMultiple { get; } = false;

    public MultipleAttribute()
    {
        IsMultiple = false;
    }
    public MultipleAttribute(bool isMultiple)
    {
        IsMultiple = isMultiple;
    }
}