using System;
using System.Threading.Tasks;
using Discord;

namespace GoogleBot.Interactions;

/// <summary>
/// Defines the class as an application module
/// </summary>
public abstract class ModuleBase
{
    /// <summary>
    /// The Context for module, when a command or method in it gets executed.
    /// May include CommandInfo, Guild, User, Channels
    /// </summary>
    public Context Context { get; set; }= new Context();
    
    public GuildConfig GuildConfig
    {
        get => Context.GuildConfig;
    }

    /// <summary>
    /// Reply to the initial message / command
    /// </summary>
    /// <param name="message">The message to reply with</param>
    /// <returns></returns>
    protected abstract Task ReplyAsync(FormattedMessage message);
    protected async Task ReplyAsync(EmbedBuilder embed, ComponentBuilder components = null)
    {
        await ReplyAsync(new FormattedMessage(embed).WithComponents(components!));
    }
    protected async Task ReplyAsync(string text)
    {
        await ReplyAsync(new FormattedMessage(text));
    }
    

    /// <summary>
    /// Sends a new message in the channel of context
    /// </summary>
    /// <param name="message">The message to send</param>
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

/// <summary>
/// Defines the class as a slash command module
/// </summary>
public abstract class CommandModuleBase : ModuleBase
{
    /// <summary>
    /// Replies to an executed command with a <see cref="FormattedMessage"/>
    /// </summary>
    /// <param name="message">The message</param>
    protected override async Task ReplyAsync(FormattedMessage message)
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
}

/// <summary>
/// Defines the class as a message command module
/// </summary>
public abstract class MessageCommandsModuleBase : ModuleBase
{
    protected override async Task ReplyAsync(FormattedMessage message)
    {
        if (Context.MessageCommand != null)
        {
            //* If (for some reason) a response hasn't started, do so
            if (!Context.MessageCommand.HasResponded)
            {
                try
                {
                    await Context.MessageCommand.DeferAsync();
                }
                catch (Exception)
                {
                    //Ignored}
                }
            }

            try
            {
                // Console.WriteLine(message.Message+ " " +message.Embed +" " +!string.IsNullOrEmpty(message.Message) + " " + !string.IsNullOrWhiteSpace(message.Message));
                await Context.MessageCommand.ModifyOriginalResponseAsync(properties =>
                {
                    properties.Embed = message.Embed?.Build();
                    properties.Components = message.Components?.Build();
                    if (!string.IsNullOrEmpty(message.Message) && !string.IsNullOrWhiteSpace(message.Message))
                    {
                        properties.Content = message.Message;
                    }
                });
            }
            catch (Exception e)
            {
                // Console.WriteLine(e.Message);
                // Console.WriteLine(e.StackTrace);
                await Context.MessageCommand.ModifyOriginalResponseAsync(properties =>
                    properties.Content = "`Couldn't respond.`");
                await (await Context.MessageCommand.GetOriginalResponseAsync()).DeleteAsync();
            }
            
        }
    }
    
}