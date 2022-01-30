using System.Collections.Generic;
using Discord;
using GoogleBot.Interactions.Commands;

namespace GoogleBot.Services;

/// <summary>
/// Additional params for a guild, like an AudioPlayer for playing sound
/// </summary>
public class GuildConfig
{
    private static readonly List<GuildConfig> GuildMaster = new();
    public AudioPlayer AudioPlayer { get; }
    public ulong Id { get; }

    private readonly List<PreconditionWatcher> watchers = new();

    public bool BotConnectedToVC => BotsVoiceChannel != null;

    public IVoiceChannel BotsVoiceChannel => AudioPlayer.VoiceChannel;

    public PreconditionWatcher GetWatcher(CommandInfo command)
    {
        PreconditionWatcher w = watchers.Find(w => w.CommandInfo.Id == command.Id);
        if (w != null) return w;
        w = new PreconditionWatcher(command, this);
        watchers.Add(w);
        return w;
    }

    public PreconditionWatcher GetWatcher(string id)
    {
        return watchers.Find(w => w.Id == id);
    }


    private GuildConfig(ulong id)
    {
        AudioPlayer = new AudioPlayer();
        Id = id;
        GuildMaster.Add(this);
    }


    /// <summary>
    /// Creates or gets existing Guild object with the ID
    /// </summary>
    /// <param name="guildId">The guilds ID</param>
    /// <returns>New or existing guild object</returns>
    public static GuildConfig Get(ulong? guildId)
    {
        if (guildId == null)
            return null;
        return GuildMaster.Find(guild => guild.Id.Equals(guildId)) ?? new GuildConfig((ulong)guildId);
    }

    // public static GuildConfig Get(SocketGuild guild)
    // {
    //     
    //     return GuildMaster.Find(g => g.Id.Equals(guild.Id)) ?? new GuildConfig((ulong)guildId);
    // }
}