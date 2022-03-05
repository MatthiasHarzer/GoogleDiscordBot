using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
    public Video? Video { get; init; }
    public PlaylistVideo[] Videos { get; init; } = Array.Empty<PlaylistVideo>();

    /// <summary>
    /// Space for some notes ¬_¬
    /// </summary>
    public string? Note { get; init; }
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
    VoiceChannelEmpty
}

/// <summary>
/// For communicating with the YouTube API v3
/// </summary>
public class YouTubeApiClient
{
    private static YouTubeApiClient? yt;
    private readonly YouTubeService service;

    /// <summary>
    /// Gets the <see cref="YouTubeApiClient"/> instance
    /// </summary>
    /// <returns></returns>
    public static YouTubeApiClient Get()
    {
        return yt ?? new YouTubeApiClient();
    }

    private YouTubeApiClient()
    {
        yt = this;
        service = new YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = Secrets.GoogleApiKey
        });
    }

    /// <summary>
    /// Searches youtube for a given search term
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <returns>A list of search results. Empty if none where found</returns>
    public async Task<List<SearchResult>> Search(string searchTerm)
    {
        var searchListRequest = service.Search.List("snippet");
        searchListRequest.Q = searchTerm;
        searchListRequest.Type = "video";
        // searchListRequest.VideoDuration = SearchResource.ListRequest.VideoDurationEnum.Short__;
        searchListRequest.MaxResults = 20;

        var response = (await searchListRequest.ExecuteAsync())?.Items ?? new List<SearchResult>();

        List<SearchResult> results = response.ToList()
            .FindAll(item => item.Snippet.LiveBroadcastContent == "none");

        return results;
    }

    /// <summary>
    /// Finds a related video given a video id
    /// </summary>
    /// <param name="videoId">The video to find related videos to</param>
    /// <returns>A video id</returns>
    public async Task<List<string>> FindRelatedVideos(string videoId)
    {
        var searchListRequest = service.Search.List("snippet");
        searchListRequest.RelatedToVideoId = videoId;
        searchListRequest.Type = "video";
        searchListRequest.MaxResults = 20;
        searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;

        var response = (await searchListRequest.ExecuteAsync())?.Items ?? new List<SearchResult>();

        List<SearchResult> results = new List<SearchResult>();
        foreach (SearchResult searchResult in response.ToList())
        {
            try
            {
                if (searchResult.Snippet.LiveBroadcastContent == "none")
                {
                    results.Add(searchResult);
                }

                ;
            }
            catch
            {
                // ignored
            }
            // if(results.Count >= 5) break;
        }


        return results.ConvertAll(input => input.Id.VideoId);
    }
}

/// <summary>
/// An audio module responsible of playing music from a query in the given voice channel 
/// </summary>
public class AudioPlayer
{
    public bool Playing;
    public IAudioClient? AudioClient;
    public readonly List<Video> Queue = new List<Video>();
    public Video? CurrentSong;
    public Video? NextTargetAutoPlaySong;
    private IVoiceChannel? voiceChannel;
    private readonly GuildConfig guildConfig;
    private YouTubeApiClient YouTubeApiClient => YouTubeApiClient.Get();

    public IVoiceChannel? VoiceChannel => voiceChannel;

    private readonly YoutubeClient youtubeExplodeClient = new YoutubeClient();

    private CancellationTokenSource audioCancellationToken = new CancellationTokenSource();

    public AudioPlayer(GuildConfig guildConfig)
    {
        this.guildConfig = guildConfig;
    }
    // private Process ffmpegProcess;

    /// <summary>
    /// Sets the auto play song depending on the currently playing song
    /// </summary>
    private async Task SetTargetAutoplaySongAsync()
    {
        if (CurrentSong == null) return;
        List<string> nextVideos = await YouTubeApiClient.FindRelatedVideos(CurrentSong.Id.Value);
        List<Video> videos = new List<Video>();
        foreach (string nextVideoId in nextVideos)
        {
            try
            {
                // Console.WriteLine("Trying to get video query");
                Video video = await youtubeExplodeClient.Videos.GetAsync(nextVideoId);
                if (video.Duration is not { TotalHours: < 1 }) continue;
                videos.Add(video);
            }
            catch (ArgumentException)
            {
            }

            if (videos.Count >= 5) break;
        }

        if (videos.Count > 0)
        {
            NextTargetAutoPlaySong = Util.GetRandom(videos);
        }
    }

    /// <summary>
    /// Add an youtube video to the queue without blocking the main thread
    /// </summary>
    /// <param name="playlistVideos">The playlistVideos to add</param>
    /// <param name="videoNotToAdd">The video to skip when occuring in playlist</param>
    private async Task AddToQueueExceptAsync(PlaylistVideo[] playlistVideos, Video? videoNotToAdd = null)
    {
        foreach (PlaylistVideo playlistVideo in playlistVideos)
        {
            if (videoNotToAdd != null && playlistVideo.Id != videoNotToAdd.Id &&
                playlistVideo.Duration is { TotalHours: <= 1 })
            {
                Video video = await youtubeExplodeClient.Videos.GetAsync(playlistVideo.Id);
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
    [SuppressMessage("ReSharper.DPA", "DPA0003: Excessive memory allocations in LOH",
        MessageId = "type: System.Byte[]")]
    private async void PlayAudioFromStream(IAudioClient audioClient, MemoryStream stream,
        Func<Task<Video?>>? onFinished = null)
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
    /// Tries to play some audio from youtube in a given voice channel
    /// </summary>
    /// <param name="query">A Youtube link or id or some search terms </param>
    /// <param name="vc">The voice channel of the user</param>
    /// <returns>An PlayReturnValue containing a State</returns>
    public async Task<PlayReturnValue> Play(string query, IVoiceChannel? vc = null)
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
        Video? video = null;
        List<PlaylistVideo> playlistVideos = new();
        bool isNewPlaylist = false;

        //* Check if video exists (only ids or urls)
        try
        {
            try
            {
                // Console.WriteLine("Trying to get video query");
                video = await youtubeExplodeClient.Videos.GetAsync(query);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Check if playlistVideos");
                playlistVideos = await youtubeExplodeClient.Playlists.GetVideosAsync(query).ToListAsync();

                if (playlistVideos.Count > 0)
                {
                    video = await youtubeExplodeClient.Videos.GetAsync(playlistVideos[0].Id);
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

            IList<SearchResult> response = await YouTubeApiClient.Search(query);
            try
            {
                // Console.WriteLine(String.Join(", ", response.ToList().Select(item=>item.Snippet.LiveBroadcastContent)));


                if (response.Count > 0)
                {
                    List<SearchResult> results = response.ToList()
                        .FindAll(item => item.Snippet.LiveBroadcastContent == "none");


                    foreach (var res in results)
                    {
                        video = await youtubeExplodeClient.Videos.GetAsync(res.Id.VideoId);
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
        
        var users = await voiceChannel.GetUsersAsync().ToListAsync().AsTask();
        int userCount = users.First()?.ToList().FindAll(u => !u.IsBot).Count ?? 0;
        if (userCount <= 0)
        {
            Console.WriteLine("VC Empty");
            Stop();
            return new PlayReturnValue
            {
                AudioPlayState = AudioPlayState.VoiceChannelEmpty
            };
        }

        _ = SetTargetAutoplaySongAsync();


        //* get stream from youtube
        var manifest = await youtubeExplodeClient.Videos.Streams.GetManifestAsync(video.Id);
        var streamInfo = manifest.GetMuxedStreams().GetWithHighestBitrate();
        Stream stream = await youtubeExplodeClient.Videos.Streams.GetAsync(streamInfo);


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
        audioCancellationToken.Cancel();
        // ffmpegProcess?.Close();
        Playing = false;
    }

    /// <summary>
    /// Plays the next song in the queue or stops the audio client (disconnects bot)
    /// </summary>
    private async Task<Video?> NextSong()
    {
        // Console.WriteLine("Next Song");
        if (audioCancellationToken.IsCancellationRequested) return null;
        CancelPlayback();

        if (Queue.Count > 0)
        {
            Video video = Queue[0];
            Queue.Remove(video);
            _ = Play(video.Id);
            return await youtubeExplodeClient.Videos.GetAsync(video.Id);
        }

        if (guildConfig.AutoPlay && NextTargetAutoPlaySong != null)
        {
            Video nextSong = NextTargetAutoPlaySong;
            _ = Play(NextTargetAutoPlaySong.Id.Value);
            return nextSong;
        }

        Stop();
        return null;
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
        NextTargetAutoPlaySong = null;

        if (AudioClient != null)
            AudioClient.StopAsync();
    }

    /// <summary>
    /// Skips the currently playing sound 
    /// </summary>
    /// <returns>The now playing song</returns>
    public async Task<Video?> Skip()
    {
        return await NextSong();
    }

    /// <summary>
    /// Clears the queue
    /// </summary>
    public void Clear()
    {
        Queue.Clear();
    }
}