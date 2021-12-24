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
        Queued,
        InvalidQuery,
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
            YoutubeClient youtube = new YoutubeClient();
            Video video;
            
            

            //* Check if video exists (only ids or urls)
            try
            {
                video = await youtube.Videos.GetAsync(query);
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
                return (State.Queued, video);
            }

            
            playing = true;
            currentSong = video;

            // Console.WriteLine("Getting song from yt");

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
        }

        public static void Skip()
        {
            taskCanceller?.Cancel();
        }
        
    }
    
}