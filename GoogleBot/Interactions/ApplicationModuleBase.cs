using System;
using System.Threading.Tasks;
using Discord;

namespace GoogleBot.Interactions;

/// <summary>
/// Defines a class as an application module
/// </summary>
public abstract class ApplicationModuleBase
{
    /// <summary>
    /// The Context for module, when a command or method in it gets executed.
    /// May include CommandInfo, Guild, User, Channels
    /// </summary>
    public Context Context { get; set; } = new Context();

    public GuildConfig GuildConfig
    {
        get => Context.GuildConfig;
    }

    public T CreateNew<T>() where T : class, new()
    {
        return new T();
    }

    /// <summary>
    /// Replies to an executed command with a <see cref="FormattedMessage"/>
    /// </summary>
    /// <param name="message">The message</param>
    protected async Task ReplyAsync(FormattedMessage message)
    {
        // Console.WriteLine($"Replying with message {message}");
        if (Context.Command != null)
        {
            //* If (for some reason) a response hasn't started, do so
            if (!Context.Command.HasResponded)
            {
                try
                {
                    await Context.Command.DeferAsync();
                }
                catch (Exception)
                {
                    //Ignored}
                }
            }

            await Context.Command.ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = message.Embed?.Build();
                properties.Components = message.Components?.Build();
                properties.Content = message.Message;
            });
        }
    }

    protected async Task ReplyAsync(EmbedBuilder embed, ComponentBuilder components = null)
    {
        await ReplyAsync(new FormattedMessage(embed).WithComponents(components!));
    }

    protected async Task ReplyAsync(string text)
    {
        await ReplyAsync(new FormattedMessage(text));
    }

    /// <summary>
    /// Send a new message in the channel of context
    /// </summary>
    /// <param name="message">The formatted message</param>
    protected async Task SendMessage(FormattedMessage message)
    {
        if (Context is { Channel: not null })
        {
            try
            {
                await Context.Command?.ModifyOriginalResponseAsync(properties => properties.Content = "`Pinged`")!;
            }
            catch (Exception)
            {
                //ignored}
            }

            await Context.Channel.SendMessageAsync(message.Message, embed: message.Embed?.Build(),
                components: message.Components?.Build());
        }
    }

    protected async Task SendMessage(string text)
    {
        await SendMessage(new FormattedMessage(text));
    }

    protected async Task SendMessage(EmbedBuilder embed)
    {
        await SendMessage(new FormattedMessage(embed));
    }
}