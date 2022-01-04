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
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace GoogleBot
{
    public class IPlayReturnValue
    {
        public State State { get; set; }
        public Video Video { get; set; }
        public string[] Videos { get; set; }
    }



    public class AudioPlayer
    {
        public bool playing;
        private IAudioClient audioClient;
        public List<Video> queue = new List<Video>();
        public Video currentSong;
        private IVoiceChannel voiceChannel;
        private ISocketMessageChannel messageChannel;
        private YoutubeClient youtube = new YoutubeClient();


        private CancellationTokenSource taskCanceller = new CancellationTokenSource();

        private async Task AddToQueueAsync(string id)
        {
            if (youtube == null) return;
            Video video = await youtube.Videos.GetAsync(id);
            queue.Add(video);
        }

        private async void PlaySoundFromMemoryStream(IAudioClient audioClient, MemoryStream memoryStream,
            Action onFinished = null)
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
                        await discord.WriteAsync(memoryStream.ToArray().AsMemory(0, memoryStream.ToArray().Length),
                            taskCanceller.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
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
                    onFinished?.Invoke();
                }
            }
        }

        public async Task<IPlayReturnValue> Play(string query, IVoiceChannel voiceChannel = null,
            ISocketMessageChannel messageChannel = null)
        {
            if (voiceChannel != null)
            {
                this.voiceChannel = voiceChannel;
            }

            if (messageChannel != null)
            {
                this.messageChannel = messageChannel;
            }

            if (this.voiceChannel == null)
            {
                return new IPlayReturnValue
                {
                    State = State.NoVoiceChannel,
                };
            }

            if (query.Length <= 0)
            {
                return new IPlayReturnValue
                {
                    State = State.InvalidQuery
                };
            }
            
            

            //* Initialize youtube streaming client
            Video video;
            List<string> playlistVideos = new();
            bool isNewPlaylist = false;

            //* Check if video exists (only ids or urls)
            try
            {
                try
                {
                    video = await youtube.Videos.GetAsync(query);
                }
                catch (ArgumentException)
                {
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
                    ApiKey = Secrets.GoogleApiKey
                });
                var searchListRequest = service.Search.List("snippet");
                searchListRequest.Q = query;
                searchListRequest.Type = "youtube#video";
                searchListRequest.MaxResults = 20;

                var response = (await searchListRequest.ExecuteAsync())?.Items;


                // Console.WriteLine(String.Join(", ", response.ToList().Select(item=>item.Snippet.LiveBroadcastContent)));

                try
                {
                    if (response != null)
                        video = await youtube.Videos.GetAsync(response.ToList()
                            .Find(item => item.Snippet.LiveBroadcastContent == "none")?.Id.VideoId);
                    else
                    {
                        throw new NullReferenceException();
                    }
                }
                catch
                {
                    return new IPlayReturnValue
                    {
                        State = State.InvalidQuery,
                    };
                   
                }
            }

            if (video.Duration is { TotalHours: > 1 })
            {
                return new IPlayReturnValue
                {
                    State = State.TooLong,
                };
              
            }

            //* If a song is already playing -> add new one to queue
            if (playing)
            {
                queue.Add(video);
                if (isNewPlaylist)
                {
                    return new IPlayReturnValue
                    {
                        State = State.QueuedAsPlaylist,
                        Video = video,
                        Videos = playlistVideos.ToArray()
                    };
                    
                }

                return new IPlayReturnValue
                {
                    State = State.Queued,
                    Video = video,
                };
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
                audioClient = await this.voiceChannel.ConnectAsync();
            }

            //* Play sound async
            PlaySoundFromMemoryStream(audioClient, memoryStream, NextSong);

            if (isNewPlaylist)
            {
                return new IPlayReturnValue
                {
                    State = State.PlayingAsPlaylist,
                    Video = video,
                    Videos = playlistVideos.ToArray()
                };
            }

            return new IPlayReturnValue
            {
                State = State.Success,
                Video = video,
            };
        }

        private void NextSong()
        {
            // Console.WriteLine("Next Song");
            if (!taskCanceller.IsCancellationRequested)
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
                voiceChannel = null;
                messageChannel = null;
            }
        }

        public void Stop()
        {
            queue.Clear();
            taskCanceller.Cancel();

            if (audioClient != null)
                audioClient.StopAsync();
            voiceChannel = null;
            currentSong = null;
        }

        public void Skip()
        {
            taskCanceller?.Cancel();
        }

        public void Clear()
        {
            queue.Clear();
        }
    }
}