using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.Services;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.YouTube.v3;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace GoogleBot
{
    public enum State
    {
        Success,
        PlayingAsPlaylist,
        QueuedAsPlaylist,
        Queued,
        InvalidQuery,
        TooLong,
        NoVoiceChannel
    }
    public class Actions
    {
        public static Search FetchGoogleQuery(string query)
        {
            CustomSearchAPIService service = new CustomSearchAPIService(new BaseClientService.Initializer
            {
                // ApplicationName = "Google Bot (Discord)",
                ApiKey = Secretes.GoogleApiKey
            });

            var listRequest = service.Cse.List();
            listRequest.Cx = Secretes.SearchEngineID;
            listRequest.Q = query;
            
            listRequest.Start = 10;

            return listRequest.Execute();

        }
        
    }

    public class AudioMaster
    {
        private static readonly Dictionary<ulong, AudioPlayer> guildMaster = new Dictionary<ulong, AudioPlayer>(); 
        

        

        
        public static async Task<(State, Video)> Play(string query, IVoiceChannel channel, ISocketMessageChannel messageChannel = null)
        {


            if (!guildMaster.ContainsKey(channel.GuildId))
            {
                guildMaster.Add(channel.GuildId, new AudioPlayer());
            }

            AudioPlayer player = guildMaster[channel.GuildId];
            return await player.Play(query, channel, messageChannel);
        }
        

        public static void Stop(IGuild guild)
        {
            guildMaster[guild.Id]?.Stop();
        }

        public static void Skip(IGuild guild)
        {
            guildMaster[guild.Id]?.Skip();
        }

        public static void Clear(IGuild guild)
        {
            guildMaster[guild.Id]?.Clear();
        }
    }
    
}