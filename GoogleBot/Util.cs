using System;
using YoutubeExplode.Videos;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Discord.Commands;
using Discord.WebSocket;

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

        public static string GetCommandFromMessage(SocketUserMessage message)
        {
            int argPos = 0;
            if (message.ToString().Length > 1 && message.HasCharPrefix('!', ref argPos))
            {
                string command = message.ToString().Split(" ")[0].Substring(argPos);

                
                foreach (CommandInfo ctx in CommandHandler._coms.Commands)
                {
                    if (ctx.Name.Equals(command) || ctx.Aliases.Contains(command))
                    {
                        return ctx.Name;
                    }
                }
                
                
            }
            return null;
        }



    }

}

