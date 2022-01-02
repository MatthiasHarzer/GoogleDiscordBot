﻿using System;
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

    public class AudioPlayer
    {
        public static bool playing;
        private static IAudioClient audioClient;
        public static List<Video> queue = new List<Video>();
        public static Video currentSong;
        private static IVoiceChannel currentChannel;
        private static ISocketMessageChannel messageChannel;
        private static YoutubeClient youtube = new YoutubeClient();
        

        private static CancellationTokenSource taskCanceller = new CancellationTokenSource();

        public static string FormattedVideoDuration(Video video)
        {
            if (video.Duration.Value.Hours == 0)
            {
                return
                    $"{video.Duration.Value.Minutes.ToString().PadLeft(2, '0')}:{video.Duration.Value.Seconds.ToString().PadLeft(2, '0')}";
            }
            else
            {
                return video.Duration.ToString();
            }
        }

        private static async Task AddToQueueAsync(string id)
        {
            if(youtube == null) return;
            Video video = await youtube.Videos.GetAsync(id);
            queue.Add(video);
        } 
        private static async void PlaySoundFromMemoryStream(IAudioClient audioClient, MemoryStream memoryStream)
        {
            await using (var discord = audioClient.CreatePCMStream(AudioApplication.Mixed))
            {
                //* Know if track end was error or other
                bool failed = false;
                try
                {

                    
                    //* Canceller to completly stop playback
                    taskCanceller = new CancellationTokenSource();
                    
                    
                    // await streamProcess.StandardOutput.BaseStream.CopyToAsync(discord, taskCanceller.Token);


                    try
                    {
                        await discord.WriteAsync(memoryStream.ToArray().AsMemory(0, memoryStream.ToArray().Length), taskCanceller.Token);
                    }catch(OperationCanceledException){}
                }
                catch (TaskCanceledException)
                {
                    //* If failed 
                    failed = true;
                }
                finally
                {
                    
                    playing = false;
                    await discord.FlushAsync();
                    // Console.WriteLine("Ending");
                    // Console.WriteLine("failed " + failed);
                    if(!failed)
                        NextSong();
                }
            }
        }
        public static async Task<(State, Video)> Play(string query, IVoiceChannel channel = null, ISocketMessageChannel messageChannel = null)
        {
            if (channel != null)
            {
                currentChannel = channel;
            }

            

            if (currentChannel == null)
            {
                return (State.NoVoiceChannel, null);
            }
            
            //* Initialize youtube streaming client
            Video video;
            bool isNewPlaylist = false;

            //* Check if video exists (only ids or urls)
            try
            {
                try
                {

                    video = await youtube.Videos.GetAsync(query);
                }
                catch(ArgumentException)
                {
                    var videos = await youtube.Playlists.GetVideosAsync(query);
                    if (videos.Count > 0)
                    {
                        video = await youtube.Videos.GetAsync(videos[0].Id);
                        isNewPlaylist = true;

                        foreach (var v in videos)
                        {
                            if (v.Id != video.Id)
                            {
                                AddToQueueAsync(v.Id);
                            }
                        }
                    }
                    else
                    {
                        throw new ArgumentException();
                    }
                }
                
            }
            catch (ArgumentException)
            {
                //* If catches, query wasn't url or id -> search youtube for video
                YouTubeService service = new YouTubeService(new BaseClientService.Initializer
                {
                    ApiKey = Secretes.GoogleApiKey
                });
                var searchListRequest = service.Search.List("snippet");
                searchListRequest.Q = query;
                searchListRequest.Type = "youtube#video";
                searchListRequest.MaxResults = 10;

                var response = (await searchListRequest.ExecuteAsync())?.Items;
                
                
                // Console.WriteLine(String.Join(", ", response.ToList().Select(item=>item.Snippet.LiveBroadcastContent)));
                
                try
                {
                    video = await youtube.Videos.GetAsync(response.ToList()
                        .Find(item => item.Snippet.LiveBroadcastContent == "none")?.Id.VideoId);
                }
                catch
                {
                    
                    return (State.InvalidQuery, null);
                }
            
            }

            //* If a song is already playing -> add new one to queue
            if (playing)
            {
                queue.Add(video);
                if (isNewPlaylist)
                {
                    return (State.QueuedAsPlaylist, video);
                }
                return (State.Queued, video);
            }

            
            playing = true;
            currentSong = video;

            // Console.WriteLine("Getting song from yt");
            if (video.Duration.Value.TotalHours > 1)
            {
                return (State.TooLong, null);
            }

            //* get stream from youtube
            var manifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
            var streamInfo = manifest.GetMuxedStreams().GetWithHighestBitrate();
            Stream stream = await youtube.Videos.Streams.GetAsync(streamInfo);
            
            

            //* Start ffmpeg process to convert yt-stream to memory stream
            Process streamProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            

            MemoryStream memoryStream = new MemoryStream();
            
            //* Attach pipe-input (yt-stream) and pipe-output (memory stream) 
         

            await Cli.Wrap("ffmpeg")
                .WithArguments("-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1")
                .WithStandardInputPipe(PipeSource.FromStream(stream))
                .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
                .ExecuteAsync();      
            
            //* If bot isn't connected -> connect 
            if (audioClient is not { ConnectionState: ConnectionState.Connected })
            {
                audioClient = await currentChannel.ConnectAsync();
            }
            
            //* Play sound async
            PlaySoundFromMemoryStream(audioClient, memoryStream);

            if (isNewPlaylist)
            {
                return (State.PlayingAsPlaylist, video);
            }
            return (State.Success, video);
        }
        
        private static void NextSong()
        {
            // Console.WriteLine("Next Song");
            if(!taskCanceller.IsCancellationRequested)
                taskCanceller?.Cancel();
            playing = false;
            if (queue.Count > 0)
            {
                Video video = queue[0];
                queue.Remove(video);
                Play(video.Id);
            }
            else
            {
                audioClient.StopAsync();
                currentChannel = null;
            }
        }

        public static void Stop()
        {
            queue.Clear();
            taskCanceller.Cancel();
            
            if (audioClient != null)
                audioClient.StopAsync();
            currentChannel = null;
            currentSong = null;
        }

        public static void Skip()
        {
            taskCanceller?.Cancel();
        }

        public static void Clear()
        {
            queue.Clear();
            
        }
    }
    
}