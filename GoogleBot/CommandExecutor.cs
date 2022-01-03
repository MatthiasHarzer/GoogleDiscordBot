using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using YoutubeExplode.Videos;

using static GoogleBot.Util;

namespace GoogleBot;

// Execute a command and return an embed as response (unified for text- and slash-commands)
public static class CommandExecutor
{
    public static readonly Dictionary<ulong, AudioPlayer> guildMaster = new Dictionary<ulong, AudioPlayer>();
    
    public static async Task<EmbedBuilder> Play(IVoiceChannel channel, string query)
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();


        if (channel == null)
        {
            embed.AddField("No voice channel", "`Please connect to voice channel first!`");
            return embed;
        }


        if (!guildMaster.ContainsKey(channel.GuildId))
        {
            guildMaster.Add(channel.GuildId, new AudioPlayer());
        }

        AudioPlayer player = guildMaster[channel.GuildId];
        (State state, Video video) = await player.Play(query, channel);


        //* User response
        switch (state)
        {
            case State.Success:
                embed.AddField("Now playing",
                    FormattedVideo(video));
                break;
            case State.PlayingAsPlaylist:
                embed.AddField("Added Playlist to queue", "⠀");
                embed.AddField("Now playing",
                    FormattedVideo(video));
                break;
            case State.Queued:
                embed.AddField("Song added to queue",
                    FormattedVideo(video));
                break;
            case State.QueuedAsPlaylist:
                embed.AddField("Playlist added to queue", "⠀");
                break;
            case State.InvalidQuery:
                embed.AddField("Query invalid", "`Couldn't find any results`");
                break;
            case State.NoVoiceChannel:
                embed.AddField("No voice channel", "`Please connect to voice channel first!`");
                break;
            case State.TooLong:
                embed.AddField("Invalid query", "Song is too long (can't be longer than 1 hour)");
                break;
        }

        return embed;
    }

    public static EmbedBuilder Skip(ulong guildId)
    {
        if (guildMaster.TryGetValue(guildId, out AudioPlayer player))
        {
            player.Skip();
        }
        return new EmbedBuilder().WithCurrentTimestamp().WithTitle("Skipping...");
    }

    public static EmbedBuilder Stop(ulong guildId)
    {
        if (guildMaster.TryGetValue(guildId, out AudioPlayer player))
        {
            player.Stop();
        }
        return new EmbedBuilder().WithCurrentTimestamp().WithTitle("Disconnecting");
    }

    public static EmbedBuilder Clear(ulong guildId)
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        int count = 0;
        if (guildMaster.TryGetValue(guildId, out AudioPlayer player))
        {
            count = player.queue.Count;
            player.Stop();
        }
        embed.AddField("Queue cleared", $"`Removed {count} items`");
        return embed;
    }

    public static EmbedBuilder Queue(ulong guildId)
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        if (guildMaster.TryGetValue(guildId, out AudioPlayer player))
        {
            Video currentSong = player.currentSong;
            ;
            List<Video> queue = player.queue;

            if (player.playing)
            {
                embed.AddField("Currently playing",
                    FormattedVideo(currentSong));
            }

            if (queue.Count > 0)
            {
                int max_length = 1024; //Discord embedField limit
                int counter = 0;

                int more_hint_len = 50;

                int approx_length = 0 + more_hint_len;

                string queue_formatted = "";

                foreach (var video in queue)
                {
                    string content =
                        $"\n\n[`{video.Title} - {video.Author} ({FormattedVideoDuration(video)})`]({video.Url})";

                    if (content.Length + approx_length > max_length)
                    {
                        queue_formatted += $"\n\n `And {queue.Count - counter} more...`";
                        break;
                    }

                    approx_length += content.Length;
                    queue_formatted += content;
                    counter++;
                }

                embed.AddField($"Queue ({queue.Count})", queue_formatted);
            }
            else
            {
                embed.AddField("Queue is empty", "Nothing to show.");
            }
        }else
        {
            embed.AddField("Queue is empty", "Nothing to show.");
        }

        return embed;
    }

    public static EmbedBuilder Help()
    {
        List<CommandInfo> _commands = CommandHandler._coms.Commands.ToList();
        EmbedBuilder embedBuilder = new EmbedBuilder
        {
            Title = "Here's a list of commands and their description:"
        };

        foreach (CommandInfo command in _commands)
        {
            // Get the command Summary attribute information
            string embedFieldText = command.Summary ?? "No description available\n";

            embedBuilder.AddField(
                $"{String.Join(" / ", command.Aliases)}  {String.Join(" ", command.Parameters.AsParallel().ToList().ConvertAll(param => $"<{param.Summary}>"))}",
                embedFieldText);
        }

        return embedBuilder;
    }
}