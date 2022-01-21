using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Google.Apis.CustomSearchAPI.v1.Data;
using YoutubeExplode.Videos;
using static GoogleBot.Util;
using GoogleBot.Interactions.CustomAttributes;

namespace GoogleBot.Interactions;

/// <summary>
/// Represents all active commands
/// </summary>
public class Commands
{
    private ExecuteContext Context { get; set; }

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
    private Task<CommandReturnValue> Test()
    {
        Console.WriteLine("In test command");
        // EmbedBuilder embed = await CommandExecutor.Execute(Context);

        return Task.FromResult(new CommandReturnValue(new EmbedBuilder().WithTitle("TEST SUCCESS")));
    }

    // Test CMD for text only response
    [Command("echo")]
    [Summary("echoes given text")]
    private Task<CommandReturnValue> Echo(params string[] text)
    {
        return Task.FromResult(new CommandReturnValue(string.Join(" ", text)));
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
            return new CommandReturnValue(embed);
        }

        AudioPlayer player = Context.GuildConfig.AudioPlayer;
        PlayReturnValue returnValue = await player.Play(query, channel);


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
                embed.AddField("No voice channel", "`Please connect to a voice channel first!`");
                break;
            case AudioPlayState.TooLong:
                embed.AddField("Invalid query", "Song is too long (can't be longer than 1 hour)");
                break;
            case AudioPlayState.JoiningChannelFailed:
                embed.AddField("Couldn't join voice channel",
                    "`Try checking the channels user limit and the bots permission.`");
                break;
            case AudioPlayState.DifferentVoiceChannels:
                embed.AddField("Invalid voice channel",
                    $"You have to be connect to the same voice channel `{returnValue.Note}` as the bot.");
                break;
        }

        return new CommandReturnValue(embed);
    }

    [Command("play-hidden")]
    [Summary(
        "Plays music in the current voice channel from an url or query, but without posting a public message o((>ω< ))o. Can only be used as a slash-command!")]
    [Private(true)]
    [SlashOnlyCommand(true)]
    public async Task<CommandReturnValue> PlayHidden([Summary("query")] params string[] q)
    {
        return await Play(q);
    }

    [Command("skip")]
    [Alias("s")]
    [Summary("Skips the current song")]
    public Task<CommandReturnValue> Skip()
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        if (Context.GuildConfig.AudioPlayer.Playing)
        {
            embed.AddField("Skipping", $"Song {FormattedVideo(Context.GuildConfig.AudioPlayer.CurrentSong)} skipped");
        }
        else
        {
            embed.AddField("Nothing to skip", "The queue is empty.");
        }

        Context.GuildConfig.AudioPlayer.Skip();


        return Task.FromResult(new CommandReturnValue(embed));
    }

    [Command("stop")]
    [Alias("leave", "disconnect", "stfu", "hdf")]
    [Summary("Disconnects the bot from the current voice channel")]
    public Task<CommandReturnValue> Stop()
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();


        if (Context.GuildConfig.AudioPlayer.Playing)
        {
            embed.AddField("Disconnecting", "Stopping audio an disconnecting from voice channel");
        }
        else
        {
            embed.AddField("Bot not connect", "No channel to disconnect from");
        }

        Context.GuildConfig.AudioPlayer.Stop();


        return Task.FromResult(new CommandReturnValue(embed));
    }

    [Command("clear")]
    [Alias("c")]
    [Summary("Clears the queue")]
    public Task<CommandReturnValue> Clear()
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();

        AudioPlayer player = Context.GuildConfig.AudioPlayer;


        player.Clear();


        embed.AddField("Queue cleared", $"Removed `{player.Queue.Count}` items");
        return Task.FromResult(new CommandReturnValue(embed));
    }

    [Command("queue")]
    [Alias("q", "list", "playing")]
    [Summary("Displays the current queue")]
    public Task<CommandReturnValue> Queue()
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        AudioPlayer player = Context.GuildConfig.AudioPlayer;

        Video currentSong = player.CurrentSong;

        List<Video> queue = player.Queue;

        if (player.Playing && currentSong != null)
        {
            embed.AddField("Currently playing",
                FormattedVideo(currentSong));
        }

        if (queue.Count > 0)
        {
            int max_length = 1024; //Discord embedField limit
            int counter = 0;

            int more_hint_len = 50;

            int approxLength = 0 + more_hint_len;

            string queueFormatted = "";

            foreach (var video in queue)
            {
                string content =
                    $"\n\n[`{video.Title} - {video.Author} ({FormattedVideoDuration(video)})`]({video.Url})";

                if (content.Length + approxLength > max_length)
                {
                    queueFormatted += $"\n\n `And {queue.Count - counter} more...`";
                    break;
                }

                approxLength += content.Length;
                queueFormatted += content;
                counter++;
            }

            embed.AddField($"Queue ({queue.Count})", queueFormatted);
        }
        else
        {
            embed.AddField("Queue is empty", "Nothing to show.");
        }


        return Task.FromResult(new CommandReturnValue(embed));
    }

    [Command("google")]
    [Alias("gl")]
    [Summary("Google something")]
    public Task<CommandReturnValue> Google([Summary("query")] params string[] q)
    {
        string query = String.Join(' ', q);
        if (query.Length <= 0)
        {
            return Task.FromResult(new CommandReturnValue(new EmbedBuilder().WithCurrentTimestamp()
                .WithTitle("Please add a search term.")));
        }

        Search result = FetchGoogleQuery(String.Join(' ', query));

        string title = $"Search results for __**{query}**__:";
        string footer =
            $"[`See approx. {result.SearchInformation.FormattedTotalResults} results on google.com 🡕`](https://goo.gl/search?{String.Join("%20", q)})";

        string reqTimeFormatted = result.SearchInformation.SearchTime != null
            ? $"{Math.Round((double)result.SearchInformation.SearchTime * 100) / 100}s"
            : "";

        EmbedBuilder embed = new EmbedBuilder
        {
            Title = title,
            Color = Color.Blue,
            Footer = new EmbedFooterBuilder
            {
                Text = reqTimeFormatted
            }
        }.WithCurrentTimestamp();

        if (result.Items == null)
        {
            embed.AddField("*Suggestions:*",
                    $"•Make sure that all words are spelled correctly.\n  •Try different keywords.\n  •Try more general keywords.\n  •Try fewer keywords.\n\n [`View on google.com 🡕`](https://goo.gl/search?{String.Join("%20", q)})")
                .WithTitle($"No results for **{query}**");
        }
        else
        {
            int approxLenght = title.Length + footer.Length + reqTimeFormatted.Length + 20;
            int max_length = 2000;

            foreach (Result item in result.Items)
            {
                string itemTitle = $"{item.Title}";
                string itemContent = $"[`>> {item.DisplayLink}`]({item.Link})\n{item.Snippet}";
                int length = itemContent.Length + itemTitle.Length;

                if (approxLenght + length < max_length)
                {
                    approxLenght += length;
                    embed.AddField(itemTitle, itemContent);
                }
                else
                {
                    break;
                }
            }

            embed.AddField("\n⠀", footer);
        }

        return Task.FromResult(new CommandReturnValue(embed));
    }


    [Command("help")]
    [Alias("h", "?")]
    [Summary("Shows a help dialog with all available commands")]
    [Private(false)]
    public Task<CommandReturnValue> Help()
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

        embedBuilder.WithFooter("The first command can always be used as a slash command (/<command>, e. g. /help)");
        return Task.FromResult(new CommandReturnValue(embedBuilder));
    }
}