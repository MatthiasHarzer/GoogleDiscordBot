#nullable enable
using System.Text.Json.Nodes;
using Discord;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace GoogleBot;

internal interface IJsonSerializable<out T>
{
    JsonObject ToJson();
    T FromJson(JsonObject jsonObject);
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