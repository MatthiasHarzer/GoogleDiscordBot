#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.CustomSearchAPI.v1.Data;
using GoogleBot.Interactions.CustomAttributes;
using GoogleBot.Interactions.Preconditions;
using GoogleBot.Services;
using CommandInfo = GoogleBot.Interactions.Commands.CommandInfo;

namespace GoogleBot.Interactions.Modules;

[DevOnly]
public class TestModule : SlashCommandModuleBase
{
    [Command("component-test")]
    [Summary("Used for testing with buttons and drop-downs")]
    public async Task ComponentTest([Summary("The buttons name")] [Name("name")] string query = "Button")
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
    [VoteConfig(buttonText: "Skip")]
    [RequiresSameVoiceChannel]
    [RequiresConnectedToVoiceChannel]
    [RequiresMajority]
    public async Task PlayTest([Summary("A search term or YT-link +")] [Name("query")] string query,
        [Summary("If the input is a playlist, shuffle it before first play")] [Name("shuffle")] bool shuffle = false)
    {
        var am = new AudioModule();
        am.SetContext(Context);
        await am.Play(query);
    }


    [Command("loop")]
    public async Task Loop([Name("over")] LoopTypes? loop)
    {
        if (loop != null)
        {
            Context.GuildConfig.LoopType = (LoopTypes)loop;
            if (loop == LoopTypes.Disabled)
            {
                await ReplyAsync("Looping is now `Disabled`");
            }
            else
            {
                await ReplyAsync($"Looping is now set to `{loop.GetDescription()}`");
            }
        }
        else
        {
            await ReplyAsync($"Looping is currently set to `{Context.GuildConfig.LoopType.GetDescription()}`");
        }
    }
}

/// <summary>
/// A general use module
/// </summary>
public class InfoModule : SlashCommandModuleBase
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

        foreach (CommandInfo command in InteractionMaster.CommandList.Where(command =>
                     !command.IsDevOnly || (command.IsDevOnly && Context.Guild.Id == Secrets.DevGuildId)))
        {
            embedBuilder.AddField(Util.FormattedCommand(command), command.Summary);
        }

        // embedBuilder.WithFooter("The first command can always be used as a slash command (/<command>, e. g. /help)");

        await ReplyAsync(embedBuilder);
    }
}

/// <summary>
/// An module responsible for playing, queueing and skipping songs.
/// </summary>
public class AudioModule : SlashCommandModuleBase
{
    [Command("play")]
    [Summary("Plays music in the current voice channel from an YT-link or query")]
    // [OldPrecondition(requiresBotConnected: true)]
    [RequiresSameVoiceChannel]
    [RequiresConnectedToVoiceChannel]
    [OptionalEphemeral]
    public async Task Play([Summary("A search term or YT-link")] [Name("query")] string query,
        [Summary("If the input is a playlist, shuffle it before first play")] [Name("shuffle")]
        bool shuffle = false)
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
        PlayReturnValue returnValue = await player.Play(query, channel, shuffle);


        //* User response
        await ReplyAsync(Responses.FromPlayReturnValue(returnValue));
    }


    [Command("skip")]
    [Summary("Skips the current song")]
    [VoteConfig(buttonText: "Skip")]
    [RequiresMajority]
    public async Task Skip()
    {
        FormattedMessage message;

        ComponentBuilder? components = null;

        if (Context.GuildConfig.AudioPlayer.Playing &&
            Context.GuildConfig.AudioPlayer.AudioClient!.ConnectionState == ConnectionState.Connected)
        {
            message = Responses.Skipped(await Context.GuildConfig.AudioPlayer.Skip());
        }
        else
        {
            message = Responses.NothingToSkip();
        }

        if (message.Embed != null) await ReplyAsync(message.Embed, components);
    }


    [Command("stop")]
    [Summary("Disconnects the bot from the current voice channel")]
    [VoteConfig(buttonText: "Stop")]
    [RequiresMajority]
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
    [AutoDeleteOldComponents]
    public async Task Queue()
    {
        AudioPlayer player = Context.GuildConfig.AudioPlayer;
        Context.DataStore.QueuePage = 0;


        // _ = Context.GuildConfig.DeleteLastInteractionOf(Context.CommandInfo);

        FormattedMessage reply = Responses.QueuePage(player, 0);
        if (player.QueuePages.Length > 1)
        {
            reply.WithComponents(
                new ComponentBuilder()
                    .WithButton("Prev. Page", Util.RandomComponentId(Context.GuildConfig, "prev-q-page"))
                    .WithButton("Next Page", Util.RandomComponentId(Context.GuildConfig, "next-q-page"))
            );
        }

        // reply.WithComponents(new ComponentBuilder().WithButton(""))

        await ReplyAsync(reply);
    }

    [Command("autoplay")]
    [Summary("Gets and/or sets autoplay. When enabled related songs will play after queue end.")]
    public async Task ToggleAutoplay([Summary("The new autoplay value")] [Name("set")] bool? autoplay)
    {
        if (autoplay == null)
        {
            await ReplyAsync(Responses.AutoPlayState(Context.GuildConfig.AutoPlay));
        }
        else
        {
            Context.GuildConfig.AutoPlay = (bool)autoplay;
            await ReplyAsync(Responses.AutoPlayStateChange(Context.GuildConfig.AutoPlay));
        }
    }

    [Command("shuffle")]
    [Summary("Shuffles the currently queued songs")]
    [RequiresSameVoiceChannel]
    public async Task Shuffle()
    {
        Context.GuildConfig.AudioPlayer.ShuffleQueue();

        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        AudioPlayer player = Context.GuildConfig.AudioPlayer;

        embed.AddField($"Shuffled queue.", $"{player.Queue.Count} songs shuffled!");
        await ReplyAsync(embed);
    }
}

/// <summary>
/// A module responsible for googleing
/// </summary>
public class GoogleModule : SlashCommandModuleBase
{
    [Command("google")]
    [Summary("Google something")]
    public async Task Google([Summary("A search term")] [Name("query")] string query)
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