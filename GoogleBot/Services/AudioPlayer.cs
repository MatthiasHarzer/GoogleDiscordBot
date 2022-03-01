using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Discord;
using Discord.Audio;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos.Streams;
using Video = YoutubeExplode.Videos.Video;

namespace GoogleBot.Services;

public class PlayReturnValue
{
    public AudioPlayState AudioPlayState { get; init; }
    public Video Video { get; init; }
    public PlaylistVideo[] Videos { get; init; }

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

    public IVoiceChannel VoiceChannel => voiceChannel;

    private readonly YoutubeClient youtube = new YoutubeClient();

    private CancellationTokenSource audioCancellationToken = new CancellationTokenSource();
    // private Process ffmpegProcess;
    
    /// <summary>
    /// Add an youtube video to the queue without blocking the main thread
    /// </summary>
    /// <param name="playlistVideos">The playlistVideos to add</param>
    /// <param name="videoNotToAdd">The video to skip when occuring in playlist</param>
    private async Task AddToQueueExceptAsync(PlaylistVideo[] playlistVideos, Video videoNotToAdd = null)
    {
        if (youtube == null) return;
        foreach (PlaylistVideo playlistVideo in playlistVideos)
        {
            if (videoNotToAdd != null && playlistVideo.Id != videoNotToAdd.Id &&
                playlistVideo.Duration is { TotalHours: <= 1 })
            {
                Video video = await youtube.Videos.GetAsync(playlistVideo.Id);
                Queue.Add(video);
            }
        }
    }

    /// <summary>
    /// Plays streams sound to an audio client from a memory stream
    /// </summary>
    /// <param name="audioClient">The Discord audio client</param>
    /// <param name="stream">The audio as a memory stream</param>
    /// <param name="onFinished">Callback when memory stream ends</param>
    private async void PlayAudioFromStream(IAudioClient audioClient, MemoryStream stream, Action onFinished = null)
    {
        await using var discord = audioClient.CreatePCMStream(AudioApplication.Mixed);
        //* Know if track end was error or other
        // Console.WriteLine("starting discord stream");
        try
        {
            //* Create a new cancellation Token for discord memory playback
            audioCancellationToken = new CancellationTokenSource();
            
            // await stream.CopyToAsync(discord, audioCancellationToken.Token);
            await discord.WriteAsync(stream.ToArray().AsMemory(0, stream.ToArray().Length),
                audioCancellationToken.Token);
        }
        catch (Exception)
        {
            // ignored (probably song just got skipped) 
        }
        finally
        {
            Playing = false;
            await discord.FlushAsync();

            onFinished?.Invoke();
        }
    }

    /// <summary>
    /// Creates an process for converting audio from an url using ffmpeg
    /// </summary>
    /// <param name="url">The url to stream the audio from</param>
    /// <returns>The created process</returns>
    // private Process CreateFfmpegProcess(string url)
    // {
    //     return Process.Start(new ProcessStartInfo
    //     {
    //         FileName = "ffmpeg",
    //         Arguments = $"-hide_banner -loglevel panic -i {url} -ac 2 -f s16le -ar 48000 pipe:1",
    //         // RedirectStandardInput = true,
    //         RedirectStandardOutput = true,
    //         UseShellExecute = false,
    //     });
    //   
    // }

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
        List<PlaylistVideo> playlistVideos = new();
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
                Console.WriteLine("Check if playlistVideos");
                playlistVideos = await youtube.Playlists.GetVideosAsync(query).ToListAsync();

                if (playlistVideos.Count > 0)
                {
                    video = await youtube.Videos.GetAsync(playlistVideos[0].Id);
                    isNewPlaylist = true;

                    _ = AddToQueueExceptAsync(playlistVideos.ToArray(), video);
                }
                else
                {
                    throw new ArgumentException();
                }
            }
        }
        catch (ArgumentException)
        {
            Console.WriteLine("Search for video on youtube");
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
        

        MemoryStream memoryStream = new MemoryStream();
        
        //* Start ffmpeg process to convert stream to memory stream
        await Cli.Wrap("ffmpeg")
            .WithArguments("-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1")
            .WithStandardInputPipe(PipeSource.FromStream(stream))
            .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
            .ExecuteAsync();
        // ffmpegProcess = CreateFfmpegProcess(streamInfo.Url);

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
                voiceChannel = null;
                return new PlayReturnValue
                {
                    AudioPlayState = AudioPlayState.JoiningChannelFailed
                };
            }
        }

        Console.WriteLine("Starting audio stream");

        //* Play sound async
        PlayAudioFromStream(AudioClient, memoryStream, NextSong);

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
    /// Cancels the current audio playback
    /// </summary>
    private void CancelPlayback()
    {
        audioCancellationToken?.Cancel();
        // ffmpegProcess?.Close();
        Playing = false;
    }
    
    /// <summary>
    /// Plays the next song in the queue or stops the audio client (disconnects bot)
    /// </summary>
    private void NextSong()
    {
        // Console.WriteLine("Next Song");
        CancelPlayback();
        if (Queue.Count > 0)
        {
            Video video = Queue[0];
            Queue.Remove(video);
            _ = Play(video.Id);
        }
        else
        {
            Stop();
        }
    }

    /// <summary>
    /// Stops all sounds from playing, disconnects the bot from the VC and clears the queue
    /// </summary>
    /// 
    public void Stop()
    {
        Queue.Clear();
        CancelPlayback();

        voiceChannel = null;
        CurrentSong = null;

        if (AudioClient != null)
            AudioClient.StopAsync();
    }

    /// <summary>
    /// Skips the currently playing sound 
    /// </summary>
    /// <returns>The now playing song</returns>
    public Video? Skip()
    {
        Video? newSong = Queue.Count > 0 ? Queue[0] : null;
        CancelPlayback();
        return newSong;
    }

    /// <summary>
    /// Clears the queue
    /// </summary>
    public void Clear()
    {
        Queue.Clear();
    }
}