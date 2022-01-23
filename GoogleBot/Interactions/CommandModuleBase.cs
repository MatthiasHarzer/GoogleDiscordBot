using System.Threading.Tasks;

namespace GoogleBot.Interactions;

public abstract class CommandModuleBase
{
    public Context Context { get; set; } = new Context();

    public void SetContext(Context context)
    {
        Context = context;
    }

    public async Task ReplyAsync(FormattedMessage message)
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