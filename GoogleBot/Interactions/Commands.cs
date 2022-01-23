#nullable enable
using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;

namespace GoogleBot.Interactions;


public class Context
{
    public Context(){}
    public Context(SocketSlashCommand command)
    {
       
        IGuildUser? guildUser = command.User as IGuildUser;
        Channel = command.Channel;
        CommandInfo = CommandMaster.GetCommandFromName(command.CommandName);
        Command = command;
        Guild = (SocketGuild?)guildUser?.Guild;
        User = command.User;
        GuildConfig = GuildConfig.Get(guildUser?.GuildId);
        VoiceChannel = guildUser?.VoiceChannel;
    }
    public SocketSlashCommand? Command {get;}
    public ISocketMessageChannel? Channel { get; }
    public CommandInfo? CommandInfo { get;  }
    public SocketGuild? Guild { get;  }
    public SocketUser? User { get;  }
    public SocketMessageComponent? Component { get; set; } = null;
    public GuildConfig? GuildConfig { get; }
    public IVoiceChannel? VoiceChannel { get;  }

}


public abstract class CommandModuleBase
{
    protected Context Context {get; set; }

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


public class TestModule : CommandModuleBase
{
    [Command("component-test")]
    public async void Play([Multiple][Summary("multiple word")][Name("input")]string query)
    {
        ComponentBuilder builder = new ComponentBuilder().WithButton("Cool button", "cool-id");

        await ReplyAsync(new FormattedMessage("POG???").WithComponents(builder));
    }

    [LinkComponentInteraction]
    public async void ComponentInteraction(SocketMessageComponent component)
    {
        Console.WriteLine("ComponentInteration linked method called");
        Console.WriteLine(component.Data.CustomId);
    }
}

