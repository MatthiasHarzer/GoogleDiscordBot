#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Google.Apis.CustomSearchAPI.v1.Data;
using GoogleBot.Interactions.CustomAttributes;
using YoutubeExplode.Videos;
using static GoogleBot.Util;

namespace GoogleBot.Interactions.Modules;

[DevOnly]
public class TestModule : ApplicationModuleBase
{
    [Command("component-test")]
    [Summary("Used for testing with buttons and drop downs")]
    public async Task ComponentTest([Multiple] [Summary("The buttons name")] [Name("name")] string query = "Button")
    {
        ComponentBuilder builder = new ComponentBuilder().WithButton(query, "button");

        await ReplyAsync(new FormattedMessage("POG???").WithComponents(builder));
    }

    [Command("ping")]
    [Summary("Ping a user")]
    public async Task Ping([Name("user")] [OptionType(ApplicationCommandOptionType.User)] SocketUser user,
        [Name("message")] [Summary("The message to append")]
        string message = "")
    {
        await SendMessage($"<@{user.Id}> \n {message}");
    }

    [LinkComponentInteraction]
    public async Task ComponentInteraction(SocketMessageComponent component)
    {
        // Console.WriteLine("ComponentInteration linked method called " + component.Data.CustomId);


        try
        {
            await component.RespondAsync("Worked!");
        }
        catch (HttpException e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }

    [LinkComponentInteraction("cool-id2")]
    public async Task IdComponentInteraction(SocketMessageComponent component)
    {
        // Console.WriteLine("ComponentInteration linked method called AT COOLD ID 2!!!!");
    }
}

/// <summary>
/// A general use module
/// </summary>
public class InfoModule : ApplicationModuleBase
{
    [Command("help")]
    [Summary("Shows a help dialog with all available commands")]
    public async Task Help()
    {
        // List<CommandInfo> _commands = CommandHandler._coms.Commands.ToList();
        EmbedBuilder embedBuilder = new EmbedBuilder
        {
            Title = "Here's a list of commands and their description:"
        };

        foreach (CommandInfo command in CommandMaster.CommandList)
        {
            // Get the command Summary attribute information
            string embedFieldText = command.Summary ?? "No description available\n";

            embedBuilder.AddField(
                $"/{command.Name}  {String.Join(" ", command.Parameters.AsParallel().ToList().ConvertAll(p => p.IsOptional ? $"[<{p.Name}>]" : $"<{p.Name}>"))}",
                embedFieldText);
        }

        // embedBuilder.WithFooter("The first command can always be used as a slash command (/<command>, e. g. /help)");

        await ReplyAsync(embedBuilder);
    }
}

/// <summary>
/// An module responsible for playing, queueing and skipping songs.
/// </summary>
public class AudioModule : ApplicationModuleBase
{
    [Command("play")]
    [Summary("Plays music in the current voice channel from an url or query")]
    [OverrideDeferAttribute(true)]
    public async Task Play([Multiple] [Summary("A search term or YT-link")] [Name("query")] string query,
        [Name("hidden")] [Summary("Whether the responds should be private ")]
        bool ephermeral = false)
    {
        Context.Command?.DeferAsync(ephemeral: ephermeral);

        IVoiceChannel? channel = Context.VoiceChannel;
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();


        if (channel == null)
        {
            embed.AddField("No voice channel", "`Please connect to voice channel first!`");

            await ReplyAsync(embed);
            return;
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

        await ReplyAsync(embed);
    }


    [Command("skip")]
    [Summary("Skips the current song")]
    public async Task Skip()
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


        await ReplyAsync(embed);
    }

    [Command("stop")]
    [Summary("Disconnects the bot from the current voice channel")]
    public async Task Stop()
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();


        if (Context.GuildConfig.AudioPlayer.Playing)
        {
            embed.AddField("Disconnecting", "Stopping audio and disconnecting from voice channel");
        }
        else
        {
            embed.AddField("Bot not connect", "No channel to disconnect from");
        }

        Context.GuildConfig.AudioPlayer.Stop();


        await ReplyAsync(embed);
    }

    [Command("clear")]
    [Summary("Clears the queue")]
    public async Task Clear()
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();

        AudioPlayer player = Context.GuildConfig.AudioPlayer;
        embed.AddField("Queue cleared", $"Removed `{player.Queue.Count}` " +
                                        (player.Queue.Count == 1 ? "item" : "items") + ".");

        player.Clear();

        await ReplyAsync(embed);
    }

    [Command("queue")]
    [Summary("Displays the current queue")]
    public async Task Queue()
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


        await ReplyAsync(embed);
    }
}

/// <summary>
/// A module responsible for googleing
/// </summary>
public class GoogleModule : ApplicationModuleBase
{
    [Command("google")]
    [Summary("Google something")]
    public async Task Google([Multiple] [Summary("A search term")] [Name("query")] string query)
    {
        if (query.Length <= 0)
        {
            EmbedBuilder e = new EmbedBuilder().WithCurrentTimestamp();
            e.AddField("No search term provided", "Please add a search term.");
            await ReplyAsync(e);
            return;
        }

        Search result = FetchGoogleQuery(String.Join(' ', query));

        string title = $"Search results for __**{query}**__:";
        string footer =
            $"[`See approx. {result.SearchInformation.FormattedTotalResults} results on google.com 🡕`](https://goo.gl/search?{HttpUtility.UrlEncode(query)})";

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
                    $"•Make sure that all words are spelled correctly.\n  •Try different keywords.\n  •Try more general keywords.\n  •Try fewer keywords.\n\n [`View on google.com 🡕`](https://goo.gl/search?{HttpUtility.UrlEncode(query)})")
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

        await ReplyAsync(embed);
    }
}