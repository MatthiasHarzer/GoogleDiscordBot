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
            Console.WriteLine("command.Data.Name " + command.Data.Name);
            EmbedBuilder embed;
            switch (command.Data.Name.ToString())
            {
                case "play":
                    Console.WriteLine("PLAY");
                    HandleSlashPlayCommand(command);
                    break;
                case "queue":
                    embed = new EmbedBuilder().WithCurrentTimestamp();

                    Video currentSong = AudioPlayer.currentSong;
                    List<Video> queue = AudioPlayer.queue;
            
                    if (AudioPlayer.playing)
                    {
                        embed.AddField("Currently playing",
                            $"[`{currentSong.Title} - {currentSong.Author} ({AudioPlayer.FormattedVideoDuration(currentSong)})`]({currentSong.Url})");
                    }

                    if (queue.Count > 0)
                    {
                        embed.AddField("Queue",
                            String.Join("\n\n",
                                queue.ConvertAll((video) =>
                                    $"[`{video.Title} - {video.Author} ({AudioPlayer.FormattedVideoDuration(video)})`]({video.Url})")));
                    }
                    else
                    {
                        embed.AddField("Queue is empty", "Nothing to show.");
                    }

                    await command.ModifyOriginalResponseAsync(properties =>
                    {
                        properties.Embed = embed.Build();
                    });
                    break;
                case "skip":
                    AudioPlayer.Skip();
                    await command.ModifyOriginalResponseAsync(properties =>
                    {
                        properties.Content = "Skipping";
                    });
                    break;
                case "stop":
                    AudioPlayer.Stop();
                    await command.ModifyOriginalResponseAsync(properties =>
                    {
                        properties.Content = "Disconnecting";
                    });
                    break;
                case "help":
                    List<CommandInfo> _commands = _coms.Commands.ToList();
                    embed = new EmbedBuilder
                    {
                        Title = "Here's a list of commands and their description:"
                    };

                    foreach (CommandInfo c in _commands)
                    {
                        // Get the command Summary attribute information
                        string embedFieldText = c.Summary ?? "No description available\n";

                        embed.AddField(
                            $"{String.Join(" / ", c.Aliases)}  {String.Join(" ", c.Parameters.AsParallel().ToList().ConvertAll(param => $"<{param.Summary}>"))}",
                            embedFieldText);
                    }
                    await command.ModifyOriginalResponseAsync(properties =>
                    {
                        properties.Embed = embed.Build();
                    });
                    break;
            }
        }

        private async Task HandleSlashPlayCommand(SocketSlashCommand command)
        {
            
            IVoiceChannel channel = (command.User as IGuildUser)?.VoiceChannel;
            
            // Console.WriteLine("Channel name: "+channel);
            string query = command.Data.Options.First()?.Value?.ToString();
            
            (State state, Video video) = await AudioPlayer.Play(query, channel);
            
            EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
            //* User response
            Console.WriteLine(state);
            switch (state)
            {
                case State.Success:
                    embed.AddField("Now playing",
                        $"[{video.Title} - {video.Author} ({AudioPlayer.FormattedVideoDuration(video)})]({video.Url})");
                    break;
                case State.Queued:
                    embed.AddField("Song added to queue",
                        $"[{video.Title} - {video.Author} ({AudioPlayer.FormattedVideoDuration(video)})]({video.Url})");
                    break;
                case State.InvalidQuery:
                    embed.AddField("Query invalid", "`Couldn't find any results`");
                    break;
                case State.NoVoiceChannel:
                    embed.AddField("No voice channel", "`Please connect to voice channel first!`");
                    break;
            }

            await command.ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = embed.Build();
            });
        }
    }
}