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