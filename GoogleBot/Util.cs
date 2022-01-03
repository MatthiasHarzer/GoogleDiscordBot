using System;
using YoutubeExplode.Videos;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace GoogleBot
{

    public static class Util
    {
        
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

        public static string FormattedVideo(Video video)
        {
            return $"[{video.Title} - {video.Author} ({FormattedVideoDuration(video)})]({video.Url})";
        }



    }
    public static class Globals
    {
        public static readonly Dictionary<ulong, AudioPlayer> guildMaster = new Dictionary<ulong, AudioPlayer>();
    }
}

