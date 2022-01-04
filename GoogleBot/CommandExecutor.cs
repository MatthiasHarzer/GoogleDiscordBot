using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Google.Apis.CustomSearchAPI.v1.Data;
using YoutubeExplode.Videos;
using static GoogleBot.Util;

namespace GoogleBot;

public class CommandExecuteContext
{
    [NotNull] public string Command { get; set; }
    public ulong GuildId { get; set; }
    public IVoiceChannel VoiceChannel { get; set; }
}

// Execute a command and return an embed as response (unified for text- and slash-commands)
public static class CommandExecutor
{
    private static readonly Dictionary<ulong, AudioPlayer> guildMaster = new Dictionary<ulong, AudioPlayer>();

    public static async Task<EmbedBuilder> Execute(CommandExecuteContext context, params object[] additionalArgs)
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        try
        {
            if (context.Command == null)
                throw new ArgumentException("No command provided");

            switch (context.Command.ToLower())
            {
                case "play":
                    return await Play(context.VoiceChannel, additionalArgs.FirstOrDefault("")?.ToString());
                case "skip":
                    return Skip(context.GuildId);
                case "queue":
                    return Queue(context.GuildId);
                case "stop":
                    return Stop(context.GuildId);
                case "clear":
                    return Clear(context.GuildId);
                case "help":
                    return Help();

                case "echo":
                    return new EmbedBuilder().WithCurrentTimestamp().WithTitle(string.Join(" ", additionalArgs));

                case "google":
                    return Google(additionalArgs.ToList().ConvertAll(a => a.ToString()).ToArray());
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.StackTrace);
            embed.AddField("Error", "Something went wrong. Please try again.");
        }

        return embed;
    }

    private static EmbedBuilder Google(params string[] _query)
    {
        string query = String.Join(' ', _query);
        if (query.Length <= 0)
        {
            return new EmbedBuilder().WithCurrentTimestamp().WithTitle("Please add a search term.");
        }

        Search result = Actions.FetchGoogleQuery(String.Join(' ', query));

        string title = $"Search results for __**{query}**__";
        string footer =
            $"[`See approx. {result.SearchInformation.FormattedTotalResults} results on google.com 🡕`](https://goo.gl/search?{String.Join("%20", _query)})";
        string reqTimeFormatted = $"{Math.Round((double)result.SearchInformation.SearchTime * 100) / 100}s";

        EmbedBuilder embed = new EmbedBuilder
        {
            Title = title,
            Color = Color.Blue,
            Footer = new EmbedFooterBuilder
            {
                Text = reqTimeFormatted
            }
        }.WithCurrentTimestamp();

        if (result?.Items == null)
        {
            embed.AddField("*Suggestions:*",
                    $"•Make sure that all words are spelled correctly.\n  •Try different keywords.\n  •Try more general keywords.\n  •Try fewer keywords.\n\n [`View on google.com 🡕`](https://goo.gl/search?{String.Join("%20", _query)})")
                .WithTitle($"No results for **{query}**");
        }
        else
        {
            int approx_lenght = title.Length + footer.Length + reqTimeFormatted.Length + 20;
            int max_length = 2000;

            foreach (Result item in result.Items)
            {
                string itemTitle = $"{item.Title}";
                string itemContent = $"[`>> {item.DisplayLink}`]({item.Link})\n{item.Snippet}";
                int length = itemContent.Length + itemTitle.Length;

                if (approx_lenght + length < max_length)
                {
                    approx_lenght += length;
                    embed.AddField(itemTitle, itemContent);
                }
                else
                {
                    break;
                }
            }

            embed.AddField("\n⠀", footer);
        }

        return embed;
    }

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
        IPlayReturnValue returnValue = await player.Play(query, channel);


        //* User response
        switch (returnValue.State)
        {
            case State.Success:
                embed.AddField("Now playing",
                    FormattedVideo(returnValue.Video));
                break;
            case State.PlayingAsPlaylist:
                embed.WithTitle($"Added {returnValue.Videos?.Length} songs to queue");
                embed.AddField("Now playing",
                    FormattedVideo(returnValue.Video));
                break;
            case State.Queued:
                embed.AddField("Song added to queue",
                    FormattedVideo(returnValue.Video));
                break;
            case State.QueuedAsPlaylist:
                embed.WithTitle($"Added {returnValue.Videos?.Length} songs to queue");
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
            player.Clear();
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

            List<Video> queue = player.queue;

            if (player.playing && currentSong != null)
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
        }
        else
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