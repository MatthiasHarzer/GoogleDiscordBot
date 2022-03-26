using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using GoogleBot.Interactions.Commands;
using GoogleBot.Interactions.Modules;

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

    /// <summary>
    /// The last response of a command
    /// </summary>
    private Dictionary<string, RestInteractionMessage> lastResponses = new Dictionary<string, RestInteractionMessage>();

    /// <summary>
    /// If set to true, recommended songs will auto play when the queue is over
    /// </summary>
    private bool autoPlayEnabled = true;

    /// <summary>
    /// Whether autoplay is enabled
    /// </summary>
    public bool AutoPlay
    {
        get => autoPlayEnabled;
        set
        {
            autoPlayEnabled = value;
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
    /// A data store for saving cross command data 
    /// </summary>
    public readonly Store DataStore = new Store();

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
    
    /// <summary>
    /// Sets a message as a last response of the a command, to use later
    /// </summary>
    /// <param name="commandInfo">The command to set the last response of</param>
    /// <param name="message">The response</param>
    public void SetLastResponseOf(CommandInfo commandInfo, RestInteractionMessage message)
    {
        lastResponses[commandInfo.Id] = message;
    }

    /// <summary>
    /// Deletes the interactions of a previous response to a command
    /// </summary>
    /// <param name="command">The command to delete the response of</param>
    public async Task DeleteLastInteractionOf(CommandInfo command)
    {
        if(!lastResponses.ContainsKey(command.Id)) return;
        await  lastResponses[command.Id].ModifyAsync(properties =>
        {
            properties.Components = new ComponentBuilder().Build();
        });
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

    /// <summary>
    /// Save guild config as file to preserve command states between restarts
    /// </summary>
    private void Export()
    {
        JsonObject jsonObject = new JsonObject
        {
            { "guildId", Id },
            { "autoPlay", AutoPlay }
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
                if (ap != null) autoPlayEnabled = (bool)ap;
            }
        }

        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            // -> something's fishy with the file or json
        }
    }
}