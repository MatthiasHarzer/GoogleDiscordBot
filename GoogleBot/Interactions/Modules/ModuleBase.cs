using System;
using System.Threading.Tasks;
using Discord;
using GoogleBot.Interactions.Context;

namespace GoogleBot.Interactions.Modules;

public interface IModuleBase
{
    /// <summary>
    /// Reply to the initial message / command
    /// </summary>
    /// <param name="message">The message to reply with</param>
    public Task ReplyAsync(FormattedMessage message);

    /// <summary>
    /// Sends a new message in the channel
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <returns></returns>
    public Task SendMessage(FormattedMessage message);
}

/// <summary>
/// The base module for <see cref="MessageCommandModuleBase"/> and <see cref="SlashCommandModuleBase"/>
/// </summary>
public abstract class ModuleBase : IModuleBase
{
    /// <summary>
    /// The context for replying and sending messages internaly
    /// </summary>
    private IContext InnerContext { get; set; }

    public async Task ReplyAsync(FormattedMessage message)
    {
        //* If (for some reason) a response hasn't started, do so
        try
        {
            await InnerContext.Respondable.DeferAsync();
        }
        catch (Exception)
        {
            //Ignored}
        }


        try
        {
            // Console.WriteLine(message.Message+ " " +message.Embed +" " +!string.IsNullOrEmpty(message.Message) + " " + !string.IsNullOrWhiteSpace(message.Message));
            await InnerContext.Respondable.ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = message.Embed?.Build();
                properties.Components = message.Components?.Build();
                if (!string.IsNullOrEmpty(message.Message) && !string.IsNullOrWhiteSpace(message.Message))
                {
                    properties.Content = message.Message;
                }
            });
        }
        catch (Exception)
        {
            // Console.WriteLine(e.Message);
            // Console.WriteLine(e.StackTrace);
            await InnerContext.Respondable.ModifyOriginalResponseAsync(properties =>
                properties.Content = "`Couldn't respond.`");
            await (await InnerContext.Respondable.GetOriginalResponseAsync()).DeleteAsync();
        }
    }

    public async Task ReplyAsync(EmbedBuilder embed, ComponentBuilder components = null)
    {
        await ReplyAsync(new FormattedMessage(embed).WithComponents(components!));
    }

    public async Task ReplyAsync(string text)
    {
        await ReplyAsync(new FormattedMessage(text));
    }


    public async Task SendMessage(FormattedMessage message)
    {
        //* If (for some reason) a response hasn't started, do so
        try
        {
            await InnerContext.Respondable.DeferAsync();
        }
        catch (Exception)
        {
            //Ignored}
        }


        try
        {
            // Console.WriteLine(message.Message+ " " +message.Embed +" " +!string.IsNullOrEmpty(message.Message) + " " + !string.IsNullOrWhiteSpace(message.Message));
            await InnerContext.Respondable.FollowupAsync(message.Message, embed: message.BuiltEmbed,
                components: message.BuiltComponents);
        }
        catch (Exception)
        {
            // Console.WriteLine(e.Message);
            // Console.WriteLine(e.StackTrace);
            // await Context.Respondable.ModifyOriginalResponseAsync(properties =>
            //     properties.Content = "`Couldn't respond.`");
            // await (await Context.Respondable.GetOriginalResponseAsync()).DeleteAsync();
        }
    }

    public async Task SendMessage(string text)
    {
        await SendMessage(new FormattedMessage(text));
    }

    public async Task SendMessage(EmbedBuilder embed, ComponentBuilder? components = null)
    {
        await SendMessage(new FormattedMessage(embed).WithComponents(components));
    }

    protected void SetInnerContext(IContext context)
    {
        InnerContext = context;
    }
}

/// <summary>
/// Defines the class as a message command module
/// </summary>
public abstract class MessageCommandModuleBase : ModuleBase
{
    /// <summary>
    /// The context in which the command gets executed
    /// </summary>
    protected MessageCommandContext Context { get; private set; }

    /// <summary>
    /// Set the context of the module
    /// </summary>
    /// <param name="context">The context</param>
    public void SetContext(MessageCommandContext context)
    {
        SetInnerContext(context);
        Context = context;
    }
}

/// <summary>
/// Defines the class as a slash command module
/// </summary>
public abstract class SlashCommandModuleBase : ModuleBase
{
    /// <summary>
    /// The context in which the command gets executed
    /// </summary>
    protected SlashCommandContext Context { get; private set; }

    /// <summary>
    /// Set the context of the module
    /// </summary>
    /// <param name="context">The context</param>
    public void SetContext(SlashCommandContext context)
    {
        SetInnerContext(context);
        Context = context;
    }
}

/// <summary>
/// Defines the class as a interaction module. Methods can link to interactions there (buttons, drop-downs)
/// <seealso cref="GoogleBot.Interactions.CustomAttributes.LinkComponentInteractionAttribute"/>
/// </summary>
public abstract class InteractionModuleBase : ModuleBase
{
    /// <summary>
    /// The context in which the interaction gets executed
    /// </summary>
    protected InteractionContext Context { get; private set; }

    /// <summary>
    /// Set the context of the module
    /// </summary>
    /// <param name="context">The new context</param>
    public void SetContext(InteractionContext context)
    {
        SetInnerContext(context);
        Context = context;
    }
}