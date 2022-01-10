using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Google.Apis.CustomSearchAPI.v1.Data;
using YoutubeExplode.Videos;
using static GoogleBot.Util;
using ParameterInfo = Discord.Commands.ParameterInfo;

namespace GoogleBot.Interactions;

    
/// <summary>
/// Return value of executed command. Can be embed or string.
/// </summary>
public class CommandReturnValue
{
    public bool IsEmbed { get; set; }
    public EmbedBuilder Embed { get; set; }
    public string Message { get; set; }

    /// <summary>
    /// Configures a new CommandReturnValue with the given embed
    /// </summary>
    /// <param name="embed">The embed to return (that should be displayed)</param>
    /// <returns>Configured CommandReturnValue for embeds</returns>
    public static CommandReturnValue From(EmbedBuilder embed)
    {
        return new CommandReturnValue
        {
            IsEmbed = true,
            Embed = embed,
        };
    }

    /// <summary>
    /// Configures a new CommandReturnValue with the given message
    /// </summary>
    /// <param name="message">The string message to return (that should be displayed)</param>
    /// <returns>Configured CommandReturnValue for a simple text message</returns>
    public static CommandReturnValue From(string message)
    {
        return new CommandReturnValue
        {
            IsEmbed = false,
            Message = message,
        };
    }
}





/// <summary>
/// Represents all active commands
/// </summary>
public class Commands
{
    private static readonly Dictionary<ulong, AudioPlayer> guildMaster = new Dictionary<ulong, AudioPlayer>();
    private ExecuteContext Context;

    /// <summary>
    /// Sets the <see cref="ExecuteContext">context</see> of the command to execute in
    /// </summary>
    /// <param name="context"></param>
    public Commands(ExecuteContext context)
    {
        Context = context;
    }
    
    
    [Command("test")]
    [Alias("t")]
    [Summary("new")]
    private async Task<CommandReturnValue> Test()
    {
        Console.WriteLine("In test command");
        // EmbedBuilder embed = await CommandExecutor.Execute(Context);

        return CommandReturnValue.From(new EmbedBuilder().WithTitle("TEST SUCCESS"));
    }
    
    [Command("play")]
    [Alias("p")]
    [Summary("Plays music in the current voice channel from an url or query")]
    public async Task<CommandReturnValue> Play([Summary("query")] params string[] q)
    {
        string query = string.Join(" ", q);

        IVoiceChannel channel = Context?.VoiceChannel;
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();


        if (channel == null)
        {
            embed.AddField("No voice channel", "`Please connect to voice channel first!`");
            return CommandReturnValue.From(embed);
        }


        if (!guildMaster.ContainsKey(channel.GuildId))
        {
            guildMaster.Add(channel.GuildId, new AudioPlayer());
        }

        AudioPlayer player = guildMaster[channel.GuildId];
        IPlayReturnValue returnValue = await player.Play(query, channel);


        //* User response
        switch (returnValue.AudioPlayState)
        {
            case AudioPlayState.Success:
                embed.AddField("Now playing",
                    FormattedVideo(returnValue.Video));
                break;
            case AudioPlayState.PlayingAsPlaylist:
                embed.WithTitle($"Added {returnValue.Videos?.Length} songs to queue");
                embed.AddField("Now playing",
                    FormattedVideo(returnValue.Video));
                break;
            case AudioPlayState.Queued:
                embed.AddField("Song added to queue",
                    FormattedVideo(returnValue.Video));
                break;
            case AudioPlayState.QueuedAsPlaylist:
                embed.WithTitle($"Added {returnValue.Videos?.Length} songs to queue");
                break;
            case AudioPlayState.InvalidQuery:
                embed.AddField("Query invalid", "`Couldn't find any results`");
                break;
            case AudioPlayState.NoVoiceChannel:
                embed.AddField("No voice channel", "`Please connect to voice channel first!`");
                break;
            case AudioPlayState.TooLong:
                embed.AddField("Invalid query", "Song is too long (can't be longer than 1 hour)");
                break;
        }

        return CommandReturnValue.From(embed);
    }
    
    [Command("skip")]
    [Alias("s")]
    [Summary("Skips the current song")]
    public async Task<CommandReturnValue> Skip()
    {
        if (guildMaster.TryGetValue(Context.Guild.Id, out AudioPlayer player))
        {
            player.Skip();
        }

        return CommandReturnValue.From(new EmbedBuilder().WithCurrentTimestamp().WithTitle("Skipping..."));
    }

    [Command("stop")]
    [Alias("leave", "disconnect", "stfu", "hdf")]
    [Summary("Disconnects the bot from the current voice channel")]
    public async Task<CommandReturnValue> Stop()
    {
        if (guildMaster.TryGetValue(Context.Guild.Id, out AudioPlayer player))
        {
            player.Stop();
        }

        return CommandReturnValue.From(new EmbedBuilder().WithCurrentTimestamp().WithTitle("Disconnecting"));
    }

    [Command("clear")]
    [Alias("c")]
    [Summary("Clears the queue")]
    public async Task<CommandReturnValue> Clear()
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        int count = 0;
        if (guildMaster.TryGetValue(Context.Guild.Id, out AudioPlayer player))
        {
            count = player.queue.Count;
            player.Clear();
        }

        embed.AddField("Queue cleared", $"`Removed {count} items`");
        return CommandReturnValue.From(embed);
    }

    [Command("queue")]
    [Alias("q","list", "playing")]
    [Summary("Displays the current queue")]
    public async Task<CommandReturnValue> Queue()
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        if (guildMaster.TryGetValue(Context.Guild.Id, out AudioPlayer player))
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

        return CommandReturnValue.From(embed);
    }
    
    [Command("google")]
    [Alias("gl")]
    [Summary("Google something")]
    public async Task<CommandReturnValue> Google([Summary("query")]params string[] _query)
    {
        string query = String.Join(' ', _query);
        if (query.Length <= 0)
        {
            return CommandReturnValue.From(new EmbedBuilder().WithCurrentTimestamp().WithTitle("Please add a search term."));
        }

        Search result = FetchGoogleQuery(String.Join(' ', query));

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

        return CommandReturnValue.From(embed);
    }




    [Command("help")]
    [Alias("h", "?")]
    [Summary("Shows a help dialog with all available commands")]
    public async Task<CommandReturnValue> Help()
    {
        // List<CommandInfo> _commands = CommandHandler._coms.Commands.ToList();
        EmbedBuilder embedBuilder = new EmbedBuilder
        {
            Title = "Here's a list of commands and their description:"
        };
    
        foreach (CommandInfo command in CommandMaster.CommandsList)
        {
            
            // Get the command Summary attribute information
            string embedFieldText = command.Summary ?? "No description available\n";
        
            embedBuilder.AddField(
                $"{String.Join(" / ", command.Aliases)}  {String.Join(" ", command.Parameters.AsParallel().ToList().ConvertAll(param => $"<{param.Summary ?? param.Name}>"))}",
                embedFieldText);
        }
    
        return CommandReturnValue.From(embedBuilder);
    }


}