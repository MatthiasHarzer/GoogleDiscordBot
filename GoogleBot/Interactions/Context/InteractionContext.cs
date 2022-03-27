using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions.Commands;
using GoogleBot.Services;

namespace GoogleBot.Interactions.Context;

/// <summary>
/// A context for modules using interaction commands
/// </summary>
public class InteractionContext : IContext
{
    public IGuildUser User { get; }
    public ISocketMessageChannel TextChannel { get; }
    public IVoiceChannel? VoiceChannel { get; }
    public CommandInfo? CommandInfo => null;
    public SocketGuild Guild { get; }
    public GuildConfig GuildConfig { get; }

    public SocketMessageComponent Component { get; }

    public SocketInteraction Respondable => Component;
    public Store DataStore => GuildConfig.DataStore;

    public InteractionContext(SocketMessageComponent component)
    {
        IGuildUser guildUser = (component.User as IGuildUser)!;
        GuildConfig = GuildConfig.Get(guildUser.GuildId);
        User = guildUser;
        TextChannel = component.Channel;
        VoiceChannel = guildUser.VoiceChannel;
        Guild = (SocketGuild)guildUser.Guild;
        Component = component;
    }
}