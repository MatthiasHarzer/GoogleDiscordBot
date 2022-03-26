using Discord;
using Discord.WebSocket;
using GoogleBot.Services;

namespace GoogleBot.Interactions.Context;

/// <summary>
/// A context for modules using interaction commands
/// </summary>
public class InteractionContext : IContext
{
    public SocketUser User { get; }
    public ISocketMessageChannel TextChannel { get; }
    public IVoiceChannel? VoiceChannel { get; }
    public SocketGuild Guild { get; }
    public GuildConfig GuildConfig { get; }

    public SocketMessageComponent Component { get; }

    public IDiscordInteraction Respondable => Component;
    public Store DataStore => GuildConfig.DataStore;

    public InteractionContext(SocketMessageComponent component)
    {
        IGuildUser guildUser = (component.User as IGuildUser)!;
        GuildConfig = GuildConfig.Get(guildUser.GuildId);
        User = component.User;
        TextChannel = component.Channel;
        VoiceChannel = guildUser.VoiceChannel;
        Guild = (SocketGuild)guildUser.Guild;
        Component = component;
    }
}