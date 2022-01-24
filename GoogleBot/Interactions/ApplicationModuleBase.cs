using System.Threading.Tasks;

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

    /// <summary>
    /// Replies to an executed command with a <see cref="FormattedMessage"/>
    /// </summary>
    /// <param name="message">The message</param>
    protected async Task ReplyAsync(FormattedMessage message)
    {
        if (Context.Command != null)
            await Context.Command.ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = message.Embed?.Build();
                properties.Components = message.Components?.Build();
                properties.Content = message.Message;
            });
    }
}