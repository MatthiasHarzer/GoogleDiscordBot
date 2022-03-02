using System.Collections.Generic;
using Discord;
using GoogleBot.Interactions.Commands;

namespace GoogleBot.Services;

/// <summary>
/// Additional params for a guild, like an AudioPlayer for playing sound
/// </summary>
public class GuildConfig
{
    private static readonly List<GuildConfig> GuildMaster = new List<GuildConfig>();
    public AudioPlayer AudioPlayer { get; }
    public ulong Id { get; }

    public bool AutoPlay { get; set; } = true;

    private readonly List<PreconditionWatcher?> watchers = new List<PreconditionWatcher?>();

    public bool BotConnectedToVc => BotsVoiceChannel != null;

    public IVoiceChannel? BotsVoiceChannel => AudioPlayer.VoiceChannel;

    public PreconditionWatcher GetWatcher(CommandInfo command)
    {
        PreconditionWatcher? watcher = watchers.Find(w => w?.CommandInfo.Id == command.Id);
        if (watcher != null) return watcher;
        watcher = new PreconditionWatcher(command, this);
        watchers.Add(watcher);
        return watcher;
    }

    public PreconditionWatcher? GetWatcher(string id)
    {
        return watchers.Find(w => w?.Id == id);
    }


    private GuildConfig(ulong id)
    {
        AudioPlayer = new AudioPlayer(this);
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
        return GuildMaster.Find(guild => guild.Id.Equals(guildId)) ?? new GuildConfig((ulong)guildId!);
    }
}

