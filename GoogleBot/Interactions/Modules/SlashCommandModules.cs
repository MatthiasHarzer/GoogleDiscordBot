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
using GoogleBot.Interactions.Commands;
using GoogleBot.Services;
using YoutubeExplode.Videos;
using CommandInfo = GoogleBot.Interactions.Commands.CommandInfo;
using Precondition = GoogleBot.Interactions.CustomAttributes.PreconditionAttribute;

namespace GoogleBot.Interactions.Modules;

[DevOnly]
public class TestModule : CommandModuleBase
{
    [Command("component-test")]
    [Summary("Used for testing with buttons and drop-downs")]
    public async Task ComponentTest([Multiple] [Summary("The buttons name")] [Name("name")] string query = "Button")
    {
        ComponentBuilder builder = new ComponentBuilder().WithButton(query, "button");

        await ReplyAsync(new FormattedMessage("POG???").WithComponents(builder));
    }

    [Command("user-count")]
    public async Task UserCount()
    {
        var users = await Context.VoiceChannel?.GetUsersAsync().ToListAsync().AsTask()!;

        int count = users.First()?.Count ?? 0;

        foreach (var user in users)
        {
            Console.WriteLine(user.Count);
        }


        await ReplyAsync("" + count);
    }

    [Command("ping")]
    [Summary("Ping a user")]
    public async Task Ping([Name("user")] [OptionType(ApplicationCommandOptionType.User)] SocketUser user,
        [Name("message")] [Summary("The message to append")]
        string message = "")
    {
        await SendMessage($"{user.Mention} \n {message}");
    }


    [Command("play-test")]
    [Summary("Play command as guild command (Dev only) ")]
    [OptionalEphemeral]
    [Precondition(requiresMajority: true, majorityVoteButtonText: "Skip", requiresBotConnected: true)]
    public async Task PlayTest([Multiple] [Summary("A search term or YT-link +")] [Name("query")] string query)
    {
        var am = new AudioModule();
        am.SetContext(Context);
        await am.Play(query);
    }
}

/// <summary>
/// A general use module
/// </summary>
public class InfoModule : CommandModuleBase
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

        foreach (CommandInfo command in InteractionMaster.CommandList)
        {
            if (command.IsDevOnly && Context.Guild != null && Context.Guild.Id != Secrets.DevGuildID) continue;
            // Get the command Summary attribute information
            embedBuilder.AddField(Util.FormattedCommand(command), command.Summary);
        }

        // embedBuilder.WithFooter("The first command can always be used as a slash command (/<command>, e. g. /help)");

        await ReplyAsync(embedBuilder);
    }
}

/// <summary>
/// An module responsible for playing, queueing and skipping songs.
/// </summary>
public class AudioModule : CommandModuleBase
{
    [Command("play")]
    [Summary("Plays music in the current voice channel from an YT-link or query")]
    [Precondition(requiresBotConnected: true)]
    [OptionalEphemeral]
    public async Task Play([Multiple] [Summary("A search term or YT-link")] [Name("query")] string query)
    {
        // Console.WriteLine("executed PLAY");
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
        await ReplyAsync(Responses.FromPlayReturnValue(returnValue));
    }


    [Command("skip")]
    [Summary("Skips the current song")]
    [Precondition(requiresMajority: true, majorityVoteButtonText: "Skip", requiresBotConnected: true)]
    public async Task Skip()
    {
        FormattedMessage message;

        ComponentBuilder? components = null;

        if (Context.GuildConfig.AudioPlayer.Playing &&
            Context.GuildConfig.AudioPlayer.AudioClient.ConnectionState == ConnectionState.Connected)
        {
            message = Responses.Skipped(Context.GuildConfig.AudioPlayer.Skip());
        }
        else
        {
            message = Responses.NothingToSkip();
        }

        await ReplyAsync(message?.Embed, components);
    }


    [Command("stop")]
    [Summary("Disconnects the bot from the current voice channel")]
    [Precondition(requiresMajority: true, majorityVoteButtonText: "Stop")]
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
            embed.AddField("Currently playing", $"[`{Util.FormattedVideo(currentSong)}`]({currentSong.Url})");
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
                string content = $"\n\n[`{Util.FormattedVideo(video)})`]({video.Url})";

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
public class GoogleModule : CommandModuleBase
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

        Search result = Util.FetchGoogleQuery(String.Join(' ', query));

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