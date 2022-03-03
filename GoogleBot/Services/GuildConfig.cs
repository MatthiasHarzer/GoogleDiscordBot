using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Discord;
using GoogleBot.Interactions.Commands;

namespace GoogleBot.Services;

/// <summary>
/// Additional params for a guild, like an AudioPlayer for playing sound
/// </summary>
public class GuildConfig
{
    private static readonly List<GuildConfig> GuildMaster = new List<GuildConfig>();
    
    /// <summary>
    /// The <see cref="GoogleBot.Services.AudioPlayer"/> instance of this guild
    /// </summary>
    public AudioPlayer AudioPlayer { get; }
    
    /// <summary>
    /// The guilds id
    /// </summary>
    public ulong Id { get; }

    public bool _autoPlay;

    /// <summary>
    /// Whether autoplay is enabled
    /// </summary>
    public bool AutoPlay
    {
        get => _autoPlay;
        set
        {
            _autoPlay = value;
            Export();
        }
    }

    /// <summary>
    /// Whether the bot is currently connected to a VC in this guild 
    /// </summary>
    public bool BotConnectedToVc => BotsVoiceChannel != null;

    /// <summary>
    /// The current bots VC, if connected
    /// </summary>
    public IVoiceChannel? BotsVoiceChannel => AudioPlayer.VoiceChannel;
    
    /// <summary>
    /// This guild precondition watchers
    /// </summary>
    private readonly List<PreconditionWatcher> watchers = new List<PreconditionWatcher>();

    /// <summary>
    /// Save guild config as file to preserve command states between restarts
    /// </summary>
    private void Export()
    {
        JsonObject jsonObject = new JsonObject
        {
            { "guildId", Id },
            {"autoPlay", AutoPlay}
        };
        if (!Directory.Exists("./guild.configs"))
        {
            Directory.CreateDirectory("./guild.configs");
        }
        File.WriteAllText($"./guild.configs/guild-{Id}.json", JsonSerializer.Serialize(jsonObject));
    }

    /// <summary>
    /// Imports guildconfig from a json file
    /// </summary>
    private void Import()
    {
        try
        {
            string content = File.ReadAllText($"./guild.configs/guild-{Id}.json");

            JsonObject? json = JsonSerializer.Deserialize<JsonObject>(content);

            if (json == null || !json.TryGetPropertyValue("id", out JsonNode? id)) return;
            if (id == null || (ulong)id != Id) return;
            
            if (json.TryGetPropertyValue("autoPlay", out JsonNode? ap))
            {
                if (ap != null) _autoPlay = (bool)ap;
            }

        }

        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            // -> something's fishy with the file or json
        }
    }
    
    /// <summary>
    /// Gets the <see cref="PreconditionWatcher"/> for a given command of this guild
    /// </summary>
    /// <param name="command">The command to get the PreconditionWatcher of</param>
    /// <returns>The PreconditoinWatcher</returns>
    public PreconditionWatcher GetWatcher(CommandInfo command)
    {
        PreconditionWatcher? watcher = watchers.Find(w => w.CommandInfo.Id == command.Id);
        if (watcher != null) return watcher;
        watcher = new PreconditionWatcher(command, this);
        watchers.Add(watcher);
        return watcher;
    }

    /// <summary>
    /// Gets the <see cref="PreconditionWatcher"/> from a given component id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public PreconditionWatcher? GetWatcher(string id)
    {
        return watchers.Find(w => w.Id == id);
    }


    private GuildConfig(ulong id)
    {
        AudioPlayer = new AudioPlayer(this);
        Id = id;
        GuildMaster.Add(this);
        Import();
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

