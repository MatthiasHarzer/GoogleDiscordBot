using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Google.Apis.CustomSearchAPI.v1.Data;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace GoogleBot
{
    public class InfoModule : ModuleBase<SocketCommandContext>
    {
        [Command("echo")]
        [Summary("Echoes a message.")]
        public Task Echo([Summary("message")] params string[] text)
        {
            return ReplyAsync(String.Join(' ', text));
        }

        [Command("help")]
        [Summary("Shows this dialog")]
        public async Task Help()
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

            await ReplyAsync(embed: embedBuilder.Build());
        }
    }

    public class GoogleModule : ModuleBase<SocketCommandContext>
    {
        [Command("google")]
        [Alias("gl")]
        [Summary("Google something")]
        public Task Request([Summary("query")] params string[] _query)
        {
            string query = String.Join(' ', _query);
            if (query.Length <= 0)
            {
                return ReplyAsync("Please add a search term.");
            }

            Search result = Actions.FetchGoogleQuery(String.Join(' ', query));

            string title = $"Search results for __**{query}**__";
            string footer =
                $"[`See approx. {result.SearchInformation.FormattedTotalResults} more results on google.com 🡕`](https://goo.gl/search?{String.Join("%20", _query)})";
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


            // List<Result> results = Actions.FetchGoogleQuery(String.Join(' ', query));
            // results.ForEach(item =>
            // {
            //     // Console.WriteLine(item.Snippet + " " + item.DisplayLink);
            // });


            return ReplyAsync(embed: embed.Build());
        }
    }

    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        
        

        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("Plays audio from an url")]
        public async Task Play([Summary("query")] params string[] _query)
        {
            string query = String.Join(" ", _query);
            var typing = Context.Channel.EnterTypingState(); //* Start typing animation

            //* Get users voice channel, if none -> error message 
            IVoiceChannel channel = (Context.User as IGuildUser)?.VoiceChannel;
            
            if (channel == null)
            {
                await ReplyAsync("Connected to a voice channel to play audio!");
                typing.Dispose();
                return;
            }

            (State state, Video video) = await AudioPlayer.Play(query, channel);
            
            EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();

            //* User response
            switch (state)
            {
                case State.Success:
                    embed.AddField("Now playing",
                        $"[{video.Title} - {video.Author} ({AudioPlayer.FormattedVideoDuration(video)})]({video.Url})");
                    break;
                case State.PlayingAsPlaylist:
                    embed.AddField("Added Playlist to queue", "⠀");
                    embed.AddField("Now playing",
                        $"[{video.Title} - {video.Author} ({AudioPlayer.FormattedVideoDuration(video)})]({video.Url})");
                    break;
                case State.Queued:
                    embed.AddField("Song added to queue",
                        $"[{video.Title} - {video.Author} ({AudioPlayer.FormattedVideoDuration(video)})]({video.Url})");
                    break;
                case State.QueuedAsPlaylist:
                    embed.AddField("Playlist added to queue","⠀");
                    break;
                case State.InvalidQuery:
                    embed.AddField("Query invalid", "`Couldn't find any results`");
                    break;
                case State.NoVoiceChannel:
                    embed.AddField("No voice channel", "`Please connect to voice channel first!`");
                    break;
            }

            await ReplyAsync(embed: embed.Build());
            typing.Dispose();
        }


        [Command("skip")]
        [Alias("s")]
        [Summary("Skips current song")]
        public Task Skip()
        {
            AudioPlayer.Skip();
            return ReplyAsync("Skipping...");
        }



        [Command("queue")]
        [Alias("q", "list")]
        [Summary("Shows current queue")]
        public async Task ListQueue()
        {
            EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();

            Video currentSong = AudioPlayer.currentSong;
            List<Video> queue = AudioPlayer.queue;
            
            if (AudioPlayer.playing)
            {
                embed.AddField("Currently playing",
                    $"[`{currentSong.Title} - {currentSong.Author} ({AudioPlayer.FormattedVideoDuration(currentSong)})`]({currentSong.Url})");
            }

            if (queue.Count > 0)
            {
                int max_length = 1024;
                int counter = 0;

                int more_hint_len = 50;
                
                int approx_length = 0 + more_hint_len;

                string queue_formatted = "";
                
                foreach (var video in queue)
                {
                    
                    string content =
                        $"\n\n[`{video.Title} - {video.Author} ({AudioPlayer.FormattedVideoDuration(video)})`]({video.Url})";

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

            await ReplyAsync(embed: embed.Build());
        }

        [Command("clear")]
        [Alias("c")]
        [Summary("Clears the queue")]
        public async Task ClearQueue()
        {
            EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
            embed.AddField("Queue cleared", $"`Removed {AudioPlayer.queue.Count} items`");
            AudioPlayer.Clear();
            await ReplyAsync(embed: embed.Build());
        }


        [Command("stop")]
        [Alias("disconnect", "stfu", "leave")]
        [Summary("Disconnects the bot from the current voice channel")]
        public Task Disconnect()
        {
            AudioPlayer.Stop();
            return ReplyAsync("Disconnected");
        }
    }
}