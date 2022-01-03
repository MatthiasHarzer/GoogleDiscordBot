using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using YoutubeExplode.Videos;
using static GoogleBot.Util;
using static GoogleBot.Globals;

namespace GoogleBot
{
    internal class Bot
    {
        private DiscordSocketClient client;
        private CommandHandler commandHandler;

        public static Task Main(string[] args)
        {
            return new Bot().MainAsync();
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task MainAsync()
        {
            client = new DiscordSocketClient();
            client.Log += Log;
            client.Ready += ClientReady;

            await client.LoginAsync(TokenType.Bot, Secretes.DiscordToken);
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task ClientReady()
        {
            // SlashCommandBuilder builder = new SlashCommandBuilder();
            //
            // builder.WithName("help");
            // builder.WithDescription("Shows all available commands");
            //
            // try
            // {
            //     await client.CreateGlobalApplicationCommandAsync(builder.Build());
            // }
            // catch(ApplicationCommandException exception)
            // {
            //     var json = JsonConvert.SerializeObject(exception, Formatting.Indented);
            //
            //     // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
            //     Console.WriteLine(json);
            // }

            CommandService commandsService = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                LogLevel = LogSeverity.Info
            });
            commandsService.Log += Log;
            commandHandler = new CommandHandler(client, commandsService);
            await commandHandler.InstallCommandsAsync();

            await client.SetGameAsync("with Google", null, ActivityType.Playing);
        }
    }

    public class CommandHandler
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        public static CommandService _coms;

        public CommandHandler(DiscordSocketClient client, CommandService commands)
        {
            this.client = client;
            this.commands = commands;
            CommandHandler._coms = commands;
        }

        public async Task InstallCommandsAsync()
        {
            client.MessageReceived += HandleCommandAsync;
            client.SlashCommandExecuted += HandleSlashCommandAsync;
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            // await commands.AddModuleAsync(typeof(GoogleModule), null);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            SocketUserMessage message = messageParam as SocketUserMessage;
            if (message == null) return;

            int argPos = 0;

            // Console.WriteLine("Received: " + message.ToString());

            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            SocketCommandContext context = new SocketCommandContext(client, message);

            await commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: null);
        }

        private async Task HandleSlashCommandAsync(SocketSlashCommand command)
        {
            await command.DeferAsync(true);
            // Console.WriteLine("command.Data.Name " + command.Data.Name);
            EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();

            AudioPlayer player = null;
            IVoiceChannel channel = (command.User as IGuildUser)?.VoiceChannel;

            if (channel == null)
            {
                embed.AddField("No voice channel", "`Please connect to voice channel first!`");
            }
            else
            {
                if (!guildMaster.ContainsKey(channel.GuildId))
                {
                    guildMaster.Add(channel.GuildId, new AudioPlayer());
                }

                player = guildMaster[channel.GuildId];

                switch (command.Data.Name.ToString())
                {
                    case "play":
                        // Console.WriteLine("PLAY");
                        HandleSlashPlayCommand(command, channel, player);
                        break;
                    case "queue":


                        if (player != null)
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
                                        $"\n\n`{FormattedVideo(currentSong)}`";

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


                        break;
                    case "skip":
                        player?.Skip();
                        embed.WithTitle("Skipping...");
                        break;
                    case "stop":
                        player?.Stop();
                        embed.WithTitle("Disconnecting");
                        break;
                    case "help":
                        List<CommandInfo> _commands = _coms.Commands.ToList();
                        embed.WithTitle("Here's a list of commands and their description:");

                        foreach (CommandInfo c in _commands)
                        {
                            // Get the command Summary attribute information
                            string embedFieldText = c.Summary ?? "No description available\n";

                            embed.AddField(
                                $"{String.Join(" / ", c.Aliases)}  {String.Join(" ", c.Parameters.AsParallel().ToList().ConvertAll(param => $"<{param.Summary}>"))}",
                                embedFieldText);
                        }


                        break;
                }
            }

            switch (command.Data.Name.ToString())
            {
                case "queue":
                case "skip":
                case "stop":
                case "help":
                    await command.ModifyOriginalResponseAsync(properties => { properties.Embed = embed.Build(); });
                    break;
            }
        }

        private async void HandleSlashPlayCommand(SocketSlashCommand command, IVoiceChannel channel, AudioPlayer player)
        {
            // Console.WriteLine("Channel name: "+channel);
            string query = command.Data.Options.First()?.Value?.ToString();

            (State state, Video video) = await player.Play(query, channel);

            EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
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

            await command.ModifyOriginalResponseAsync(properties => { properties.Embed = embed.Build(); });
        }
    }
}