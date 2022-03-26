﻿using System;
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
    public List<Video> Queue = new List<Video>();
    public bool QueueComplete { get; private set; } = true;

    public bool AutoPlayEnabled => guildConfig.AutoPlay;

    public string[] QueuePages
    {
        get
        {
            
            List<string> pages = new List<string>();
            // pages = new List<string>()
            // {
            //     "Page 1",
            //     "page 2",
            //     "Page 3"
            // };
            int index = 0;


            while (true)
            {
                // break;
                const int maxLength = 1024; //Discord embedField limit
                int counter = 0;

                const int moreHintLen = 50;

                int approxLength = 0 + moreHintLen;

                string queueFormatted = "";

                foreach (var video in Queue.GetRange(index, Queue.Count - index))
                {
                    string content = $"\n\n{Util.FormattedLinkedVideo(video)}";

                    if (content.Length + approxLength > maxLength)
                    {
                        // queueFormatted += $"\n\n `And {Queue.Count - counter} more...`";
                        break;
                    }

                    approxLength += content.Length;
                    queueFormatted += content;
                    counter++;
                }

                index += counter;
                
                pages.Add(queueFormatted);
                if (index < Queue.Count) continue;
                break;
            }
            
            return pages.ToArray();
        }
    }
    public Video? CurrentSong { get; private set; }
    public Video? NextTargetSong { get; private set; }
    
    
    private IVoiceChannel? voiceChannel;
    private readonly GuildConfig guildConfig;
    private YouTubeApiClient YouTubeApiClient => YouTubeApiClient.Get();

    public IVoiceChannel? VoiceChannel => voiceChannel;

    private readonly YoutubeClient youtubeExplodeClient = new YoutubeClient();

    private CancellationTokenSource audioCancellationToken = new CancellationTokenSource();

    private Task? targetSongSetter;

    public AudioPlayer(GuildConfig guildConfig)
    {
        this.guildConfig = guildConfig;
    }
    // private Process ffmpegProcess;

    /// <summary>
    /// Sets the next song depending on queue or the currently playing song
    /// </summary>
    private void SetTargetSong()
    {
        targetSongSetter?.Dispose();
        targetSongSetter = _SetTargetSongAsync();
        
    }

    private async Task _SetTargetSongAsync()
    {
        NextTargetSong = null;
        if (Queue.Count > 0)
        {
            NextTargetSong = Queue.First();
        }
        else if(CurrentSong != null)
        {
            // Find a recommended video
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
                NextTargetSong = Util.GetRandom(videos);
            }
        }
    }

    /// <summary>
    /// Add an youtube video to the queue without blocking the main thread
    /// </summary>
    /// <param name="playlistVideos">The playlistVideos to add</param>
    /// <param name="videoNotToAdd">The video to skip when occuring in playlist</param>
    private async Task AddToQueueExceptAsync(PlaylistVideo[] playlistVideos, Video? videoNotToAdd = null)
    {
        QueueComplete = false;
        foreach (PlaylistVideo playlistVideo in playlistVideos)
        {
            if (videoNotToAdd == null || playlistVideo.Id == videoNotToAdd.Id ||
                playlistVideo.Duration is not { TotalHours: <= 1 }) continue;
            Video video = await youtubeExplodeClient.Videos.GetAsync(playlistVideo.Id);
            Queue.Add(video);
        }

        QueueComplete = true;
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
            SetTargetSong();
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

        SetTargetSong();

        MemoryStream memoryStream = new MemoryStream();
   
        //* get stream from youtube
        var manifest = await youtubeExplodeClient.Videos.Streams.GetManifestAsync(video.Id);
        
        // var streamInfo = manifest.GetMuxedStreams().GetWithHighestBitrate();
        var s = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        Stream stream = await youtubeExplodeClient.Videos.Streams.GetAsync(s);
        
        // Console.WriteLine(stream.);
        // url =
        //     "https://rr4---sn-h0jelnes.googlevideo.com/videoplayback?expire=1647618142&ei=_lM0YtybI4bv7gOh8bSQBA&ip=2003%3Ad2%3Af703%3Af07%3Ad78%3A1c42%3A8e92%3Aa2ae&id=o-ANiYSRXUDxoj9aFqr-Oc5seDooeYLUl9RoWskadNoQQS&itag=251&source=youtube&requiressl=yes&mh=RH&mm=31%2C29&mn=sn-h0jelnes%2Csn-h0jeenl6&ms=au%2Crdu&mv=m&mvi=4&pl=36&ctier=A&pfa=5&initcwndbps=1313750&hightc=yes&vprv=1&mime=audio%2Fwebm&ns=PovBucmpJChzSnfye4EGT9IG&gir=yes&clen=10131779&dur=659.781&lmt=1638011164437871&mt=1647596213&fvip=4&keepalive=yes&fexp=24001373%2C24007246&c=WEB&txp=5431432&n=PVzQzDpDesw15g&sparams=expire%2Cei%2Cip%2Cid%2Citag%2Csource%2Crequiressl%2Cctier%2Cpfa%2Chightc%2Cvprv%2Cmime%2Cns%2Cgir%2Cclen%2Cdur%2Clmt&sig=AOq0QJ8wRAIgH-jqQ2RJiQyYFiVqKe5Qw1HhA8GsHC1Jy_WFhK8Ja5MCICyPJLYPGVscCKaSkGa7w1UL7PI2h44l_c33CIPnyCi6&lsparams=mh%2Cmm%2Cmn%2Cms%2Cmv%2Cmvi%2Cpl%2Cinitcwndbps&lsig=AG3C_xAwRQIhALfpQRmSZKHX6T4Q2hT2V1RRS9UWrKSBdQAk-0q6R1IFAiBEkG-IoeCi9S-hG08tHjQ1UgI5-Jm0hGaubQHNWfl2Dw%3D%3D&alr=yes&cpn=0edB5ygKSPnmH557&cver=2.20220317.00.00&rn=11&rbuf=2729";

        //* Start ffmpeg process to convert stream to memory stream
       await Cli.Wrap("ffmpeg")
            .WithArguments($"-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1")
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
            Video? video;
            if (NextTargetSong == null && targetSongSetter != null)
            {
                await targetSongSetter;
                video = NextTargetSong;
            }
            else
            {
                video = Queue[0];
            }
            Queue.Remove(video!);
            _ = Play(video!.Id);
            return video;
        }else if (guildConfig.AutoPlay && targetSongSetter != null)
        {
            if (NextTargetSong == null) await targetSongSetter;
            
            Video nextSong = NextTargetSong!;
            _ = Play(NextTargetSong!.Id.Value);
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
        NextTargetSong = null;

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
        SetTargetSong();
    }

    public void ShuffleQueue()
    {
        Random rng = new Random();
        Queue = Queue.OrderBy(q => rng.Next()).ToList();
        SetTargetSong();
    }
}