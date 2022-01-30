using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions.Commands;

namespace GoogleBot.Interactions.Context;

public interface IContext
{
    /// <summary>
    /// The user who triggered the interaction
    /// </summary>
    public SocketUser User { get; }
    
    /// <summary>
    /// The Text Channel where the interaction was triggerd
    /// </summary>
    public ISocketMessageChannel TextChannel { get; }
    
    /// <summary>
    /// The users voice channel, if the user is connected to one
    /// </summary>
    public IVoiceChannel? VoiceChannel { get; }
    
    /// <summary>
    /// The guild where the interaction takes place
    /// </summary>
    public SocketGuild Guild { get; }
    
    /// <summary>
    /// The <see cref="GoogleBot.GuildConfig"/> of the <see cref="Guild"/>
    /// </summary>
    public GuildConfig GuildConfig { get; }
    
    public IDiscordInteraction Respondable { get; }
    
}

public interface ICommandContext : IContext
{
    /// <summary>
    /// The <see cref="Commands.CommandInfo"/> of interaction
    /// </summary>
    public CommandInfo CommandInfo { get; }
    
    

    public bool IsEphemeral => false;
}