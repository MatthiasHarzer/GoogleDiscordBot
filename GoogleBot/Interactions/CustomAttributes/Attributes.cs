using System;

namespace GoogleBot.Interactions.CustomAttributes;

/// <summary>
/// Defines, whether the commands reply should be private or public
/// </summary>
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