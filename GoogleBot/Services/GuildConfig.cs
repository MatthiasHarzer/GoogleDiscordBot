﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using GoogleBot.Interactions.Commands;

namespace GoogleBot.Services;

public enum LoopTypes
{
    Disabled,
    Song,
}

/// <summary>
/// Additional params for a guild, like an AudioPlayer for playing sound
/// </summary>
public class GuildConfig
{
    private static readonly List<GuildConfig> GuildMaster = new List<GuildConfig>();

    public SocketGuild Guild { get;}

    public GuildTimer Timer { get; } = new GuildTimer();

    /// <summary>
    /// The <see cref="GoogleBot.Services.AudioPlayer"/> instance of this guild
    /// </summary>
    public AudioPlayer AudioPlayer { get; }
    
    public VoteService VoteService { get; }

    /// <summary>
    /// The guilds id
    /// </summary>
    public ulong Id => Guild.Id;

    /// <summary>
    /// The last response of a command
    /// </summary>
    private readonly Dictionary<string, RestInteractionMessage> lastResponses = new Dictionary<string, RestInteractionMessage>();

    /// <summary>
    /// If set to true, recommended songs will auto play when the queue is over
    /// </summary>
    private bool autoPlayEnabled = false;

    /// <summary>
    /// Whether autoplay is enabled
    /// </summary>
    public bool AutoPlay
    {
        get => autoPlayEnabled;
        set
        {
            autoPlayEnabled = value;
            if(autoPlayEnabled)
                AudioPlayer.SetTargetSong();
            Export();
        }
    }

    private LoopTypes loopType = LoopTypes.Disabled;
    /// <summary>
    /// Depending on it, songs will loop over (queue, song, disblaed)
    /// </summary>
    public LoopTypes LoopType
    {
        get => loopType;
        set
        {
            loopType = value;
            Export();
        }
    }

    /// <summary>
    /// The current bots VC, if connected
    /// </summary>
    public IVoiceChannel? BotsVoiceChannel => Guild.GetUser(Globals.Client.CurrentUser.Id)?.VoiceChannel;
    

    
    /// <summary>
    /// A data store for saving cross command data 
    /// </summary>
    public readonly Store DataStore = new Store();
    

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
        if (!lastResponses.ContainsKey(command.Id)) return;
        try
        {
            await lastResponses[command.Id].ModifyAsync(properties =>
            {
                properties.Components = new ComponentBuilder().Build();
            });
        }
        catch
        {
            //ignored
        }
    }


    private GuildConfig(SocketGuild guild)
    {
        Guild = guild;
        AudioPlayer = new AudioPlayer(this);
        VoteService = new VoteService(this);
        GuildMaster.Add(this);
        Import();
    }


    /// <summary>
    /// Creates or gets existing Guild object with the ID
    /// </summary>
    /// <param name="guild">The guilds ID</param>
    /// <returns>New or existing guild object</returns>
    public static GuildConfig Get(SocketGuild guild)
    {
        return GuildMaster.Find(g => g.Id.Equals(guild.Id)) ?? new GuildConfig(guild);
    }

    /// <summary>
    /// Save guild config as file to preserve command states between restarts
    /// </summary>
    private void Export()
    {
        JsonObject jsonObject = new JsonObject
        {
            { "guildId", Id },
            { "autoPlay", AutoPlay },
            { "loopType", loopType.ToInt()}
        };

        File.WriteAllText(Storage.GetGuildConfigFileOf(this), JsonSerializer.Serialize(jsonObject));
    }

    /// <summary>
    /// Imports guildconfig from a json file
    /// </summary>
    private void Import()
    {
        try
        {
            string content = File.ReadAllText(Storage.GetGuildConfigFileOf(this));

            JsonObject? json = JsonSerializer.Deserialize<JsonObject>(content);

            if (json == null || !json.TryGetPropertyValue("guildId", out JsonNode? id)) return;
            if (id == null || (ulong)id != Id) return;

            if (json.TryGetPropertyValue("autoPlay", out JsonNode? ap))
            {
                if (ap != null) autoPlayEnabled = (bool)ap;
            }
            if (json.TryGetPropertyValue("loopType", out JsonNode? lt))
            {
                if (lt != null) loopType = (LoopTypes)(int)lt;
            }
        }

        catch (Exception)
        {
            // Console.WriteLine(e.Message);
            // Console.WriteLine(e.StackTrace);
            // -> something's fishy with the file or json
        }
    }
}