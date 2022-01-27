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
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos.Streams;
using Video = YoutubeExplode.Videos.Video;
// ReSharper disable ParameterHidesMember

namespace GoogleBot
{
    public class PlayReturnValue
    {
        public AudioPlayState AudioPlayState { get; init; }
        public Video Video { get; init; }
        public string[] Videos { get; init; }

        /// <summary>
        /// Space for some notes ¬_¬
        /// </summary>
        public string Note { get; init; } = null;
    }

    public enum AudioPlayState
    {
        Success,
        PlayingAsPlaylist,
        QueuedAsPlaylist,
        Queued,
        InvalidQuery,
        TooLong,
        NoVoiceChannel,
        JoiningChannelFailed,
        DifferentVoiceChannels,
        CancelledEarly,
    }


    /// <summary>
    /// An audio module responsible of playing music from a query in the given voice channel 
    /// </summary>
    public class AudioPlayer
    {
        public bool Playing = false;
        public IAudioClient AudioClient;
        public readonly List<Video> Queue = new List<Video>();
        public Video CurrentSong;
        private IVoiceChannel voiceChannel;
        private readonly YoutubeClient youtube = new YoutubeClient();

        private CancellationTokenSource taskCanceller = new CancellationTokenSource();

        /// <summary>
        /// Add an youtube video to the queue without blocking the main thread
        /// </summary>
        /// <param name="id">The id of the video</param>
        private async Task AddToQueueAsync(string id)
        {
            if (youtube == null) return;
            Video video = await youtube.Videos.GetAsync(id);
            Queue.Add(video);
        }

        /// <summary>
        /// Plays streams sound to an audio client from a memory stream
        /// </summary>
        /// <param name="audioClient">The Discord audio client</param>
        /// <param name="memoryStream">The audio as a memory stream</param>
        /// <param name="onFinished">Callback when memory stream ends</param>
        private async void PlaySoundFromMemoryStream(IAudioClient audioClient, MemoryStream memoryStream,
            Action onFinished = null)
        {
            await using (var discord = audioClient.CreatePCMStream(AudioApplication.Mixed))
            {
                //* Know if track end was error or other
                // Console.WriteLine("starting discord stream");
                try
                {
                    //* Canceller to completly stop playback
                    taskCanceller = new CancellationTokenSource();


                    // await streamProcess.StandardOutput.BaseStream.CopyToAsync(discord, taskCanceller.Token);


                    try
                    {
                        await discord.WriteAsync(memoryStream.ToArray().AsMemory(0, memoryStream.ToArray().Length),
                            taskCanceller.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Console.WriteLine("OperationCanceledException " + e.Message + e.StackTrace);
                    }
                }
                catch (TaskCanceledException)
                {
                    //* If failed 

                    // Console.WriteLine("TaskCanceledException " + e.Message + e.StackTrace);
                }
                finally
                {
                    Playing = false;
                    await discord.FlushAsync();
                    // Console.WriteLine("Ending");
                    // Console.WriteLine("failed " + failed);
                    onFinished?.Invoke();
                }
            }
        }

        /// <summary>
        /// Tries to play some audio from youtube in a given voice channel
        /// </summary>
        /// <param name="query">A Youtube link or id or some search terms </param>
        /// <param name="vc">The voice channel of the user</param>
        /// <returns>An PlayReturnValue containing a State</returns>
        public async Task<PlayReturnValue> Play(string query, IVoiceChannel vc = null)
        {
            if (vc != null)
            {
                if (this.voiceChannel == null)
                    this.voiceChannel = vc;
                else if (!vc.Equals(this.voiceChannel))
                {
                    return new PlayReturnValue
                    {
                        AudioPlayState = AudioPlayState.DifferentVoiceChannels,
                        Note = this.voiceChannel.Name
                    };
                }
            }


            if (voiceChannel == null)
            {
                return new PlayReturnValue
                {
                    AudioPlayState = AudioPlayState.NoVoiceChannel,
                };
            }

            if (query.Length <= 0)
            {
                return new PlayReturnValue
                {
                    AudioPlayState = AudioPlayState.InvalidQuery
                };
            }


            //* Initialize youtube streaming client
            Video video = null;
            List<string> playlistVideos = new();
            bool isNewPlaylist = false;

            //* Check if video exists (only ids or urls)
            try
            {
                try
                {
                    // Console.WriteLine("Trying to get video query");
                    video = await youtube.Videos.GetAsync(query);
                }
                catch (ArgumentException)
                {
                    Console.WriteLine("ArgumentException 1");
                    var videos = await youtube.Playlists.GetVideosAsync(query);
                    if (videos.Count > 0)
                    {
                        video = await youtube.Videos.GetAsync(videos[0].Id);
                        playlistVideos = videos.AsParallel().ToList().ConvertAll(v => v.Id.ToString());
                        isNewPlaylist = true;

                        foreach (var v in videos)
                        {
                            if (v.Duration != null && v.Id != video.Id && v.Duration.Value.TotalHours <= 1)
                            {
                                //* no warning
                                _ = AddToQueueAsync(v.Id);
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
                Console.WriteLine("ArgumentException 2");
                //* If catches, query wasn't url or id -> search youtube for video
                YouTubeService service = new YouTubeService(new BaseClientService.Initializer
                {
                    ApiKey = Secrets.GoogleApiKey
                });
                var searchListRequest = service.Search.List("snippet");
                searchListRequest.Q = query;
                searchListRequest.Type = "video";
                // searchListRequest.VideoDuration = SearchResource.ListRequest.VideoDurationEnum.Short__;
                searchListRequest.MaxResults = 20;

                try
                {
                    var response = (await searchListRequest.ExecuteAsync())?.Items;


                    // Console.WriteLine(String.Join(", ", response.ToList().Select(item=>item.Snippet.LiveBroadcastContent)));


                    if (response != null)
                    {
                        List<SearchResult> results = response.ToList()
                            .FindAll(item => item.Snippet.LiveBroadcastContent == "none");


                        foreach (var res in results)
                        {
                            video = await youtube.Videos.GetAsync(res.Id.VideoId);
                            if (video.Duration is { TotalHours: > 1 })
                            {
                                continue;
                            }

                            break;
                        }

                        if (video == null)
                            throw new NullReferenceException();
                    }
                    else
                    {
                        throw new NullReferenceException();
                    }
                }
                catch
                {
                    return new PlayReturnValue
                    {
                        AudioPlayState = AudioPlayState.InvalidQuery,
                    };
                }
            }

            if (video.Duration is { TotalHours: > 1 })
            {
                return new PlayReturnValue
                {
                    AudioPlayState = AudioPlayState.TooLong,
                };
            }

            //* If a song is already playing -> add new one to queue
            if (Playing)
            {
                Queue.Add(video);
                if (isNewPlaylist)
                {
                    return new PlayReturnValue
                    {
                        AudioPlayState = AudioPlayState.QueuedAsPlaylist,
                        Video = video,
                        Videos = playlistVideos.ToArray()
                    };
                }

                return new PlayReturnValue
                {
                    AudioPlayState = AudioPlayState.Queued,
                    Video = video,
                };
            }


            Playing = true;
            CurrentSong = video;

            //* get stream from youtube
            var manifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
            var streamInfo = manifest.GetMuxedStreams().GetWithHighestBitrate();
            Stream stream = await youtube.Videos.Streams.GetAsync(streamInfo);


            // Console.WriteLine("Creating stream process");
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
            if (AudioClient is not { ConnectionState: ConnectionState.Connected })
            {
                Console.WriteLine("Connecting to voicechannel");
                try
                {
                    AudioClient = await this.voiceChannel.ConnectAsync();
                }
                catch (Exception)
                {
                    
                    Playing = false;
                    CurrentSong = null;
                    if (this.voiceChannel == null)
                        return new PlayReturnValue
                        {
                            AudioPlayState = AudioPlayState.CancelledEarly
                        };
                    return new PlayReturnValue
                    {
                        AudioPlayState = AudioPlayState.JoiningChannelFailed
                    };
                }
            }

            Console.WriteLine("Starting audio stream");

            //* Play sound async
            PlaySoundFromMemoryStream(AudioClient, memoryStream, NextSong);

            if (isNewPlaylist)
            {
                return new PlayReturnValue
                {
                    AudioPlayState = AudioPlayState.PlayingAsPlaylist,
                    Video = video,
                    Videos = playlistVideos.ToArray()
                };
            }


            return new PlayReturnValue
            {
                AudioPlayState = AudioPlayState.Success,
                Video = video,
            };
        }

        /// <summary>
        /// Plays the next song in the queue or stops the audio client (disconnects bot)
        /// </summary>
        private void NextSong()
        {
            // Console.WriteLine("Next Song");
            if (!taskCanceller.IsCancellationRequested)
                taskCanceller?.Cancel();
            Playing = false;
            if (Queue.Count > 0)
            {
                Video video = Queue[0];
                Queue.Remove(video);
                _ = Play(video.Id);
            }
            else
            {
                AudioClient.StopAsync();
                voiceChannel = null;
            }
        }

        /// <summary>
        /// Stops all sounds from playing, disconnects the bot from the VC and clears the queue
        /// </summary>
        /// 
        public void Stop()
        {
            Queue.Clear();
            taskCanceller.Cancel();

            voiceChannel = null;
            CurrentSong = null;
            Playing = false;

            if (AudioClient != null)
                AudioClient.StopAsync();
        }

        /// <summary>
        /// Skips the currently playing sound 
        /// </summary>
        public void Skip()
        {
            taskCanceller?.Cancel();
            Playing = false;
        }

        /// <summary>
        /// Clears the queue
        /// </summary>
        public void Clear()
        {
            Queue.Clear();
        }
    }
}