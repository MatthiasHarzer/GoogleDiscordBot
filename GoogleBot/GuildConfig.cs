using System;
using System.Collections.Generic;

namespace GoogleBot;

public class GuildConfig
{
    private static readonly List<GuildConfig> GuildMaster = new();
    public AudioPlayer AudioPlayer { get; }
    public ulong Id { get; }
    public string Prefix
    {
        get => "!";
    } // Can be used in the future

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
}

