using System;
using System.Collections.Generic;
using System.Timers;
using static GoogleBot.Util;

namespace GoogleBot;

/// <summary>
/// Additional params for a guild, like an AudioPlayer for playing sound
/// </summary>
public class GuildConfig
{
    private static readonly List<GuildConfig> GuildMaster = new();
    public AudioPlayer AudioPlayer { get; }
    public ulong Id { get; }


    public List<ulong> VotedUsers { get; set; } = new();
    public int RequiredVotes { get; set; } = 0;

    public string ValidSkipVoteId { get; set; } = string.Empty;

    public void GenerateSkipId()
    {
        ValidSkipVoteId = $"sv-{Id}-{DateTime.Now.TimeOfDay.TotalMilliseconds}-{RandomString()}";
    }

    public void InvalidateVoteData()
    {
        VotedUsers.Clear();
        RequiredVotes = 0;
        ValidSkipVoteId = null;
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