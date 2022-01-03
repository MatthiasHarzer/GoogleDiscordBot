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

            await client.LoginAsync(TokenType.Bot, Secrets.DiscordToken);
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
            EmbedBuilder embed = null;


            IVoiceChannel channel = (command.User as IGuildUser)?.VoiceChannel;
            ulong guildId = ((IGuildUser)command.User).GuildId;


            switch (command.Data.Name.ToString())
            {
                case "play":
                    // Console.WriteLine("PLAY");
                    HandleSlashPlayCommand(command, channel);
                    break;
                case "queue":
                    embed = CommandExecutor.Queue(guildId);
                    break;
                case "skip":
                    embed = CommandExecutor.Skip(guildId);
                    break;
                case "stop":
                    embed = CommandExecutor.Stop(guildId);
                    break;
                case "help":
                    embed = CommandExecutor.Help();
                    break;
            }

            if (embed != null)
                await command.ModifyOriginalResponseAsync(properties => { properties.Embed = embed.Build(); });
        }

        private async void HandleSlashPlayCommand(SocketSlashCommand command, IVoiceChannel channel)
        {
            // Console.WriteLine("Channel name: "+channel);
            string query = command.Data.Options.First()?.Value?.ToString();

            EmbedBuilder embed = await CommandExecutor.Play(channel, query);

            await command.ModifyOriginalResponseAsync(properties => { properties.Embed = embed.Build(); });
        }
    }
}