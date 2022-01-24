using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using static GoogleBot.Util;
using GoogleBot.Interactions;
using Newtonsoft.Json;


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
            CommandService commandsService = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                LogLevel = LogSeverity.Info
            });
            commandsService.Log += Log;
            commandHandler = new CommandHandler(client, commandsService);
            await commandHandler.InstallCommandsAsync();

            await client.SetGameAsync("with Google", type: ActivityType.Playing);

            // Commands.testing();
            // CommandMaster.InstantiateCommands();
            CommandMaster.MountModules();

            Console.WriteLine("Client Ready");
            try
            {
                await RegisterCommandsAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }


            // Console.WriteLine(string.Join(", ", CommandHandler._coms.Commands.ToList().ConvertAll(c=>c.Name)));

            // _ = InitSlashCommandsAsync();
            // Console.WriteLine(string.Join(", ",CommandHandler._coms.Commands.AsParallel().ToList().ConvertAll(c=>String.Join(" / ", c.Aliases))));
        }

        private async Task RegisterCommandsAsync()
        {
            ulong guildId = Secrets.DevGuildID;

            List<CommandInfo> newOrChangedCommands = new List<CommandInfo>();

            List<CommandInfo> existingCommands = CommandMaster.ImportCommands();
            
            

            Console.WriteLine(existingCommands.Count + " " + CommandMaster.CommandList.Count);

            foreach (CommandInfo command in CommandMaster.CommandList)
            {
                foreach (CommandInfo existingCommand in existingCommands)
                {
                    if (command.Name.Equals(existingCommand.Name))
                    {
                        // Console.WriteLine("comparing " + command + " vs " + existingCommand);
                        if (!ApproximatelyEqual(command, existingCommand))
                        {
                            //* Not working for now
                            newOrChangedCommands.Add(command);
                        }
                    }
                }
            }

            
            if (newOrChangedCommands.Count <= 0)
            {
                return;
            }

            Console.WriteLine("New or changed commands:");
            Console.WriteLine(string.Join(", ", newOrChangedCommands.ConvertAll(c => $"{c.Name} - {c.Summary}")));
            
            var guild = client.GetGuild(guildId);

            List<ApplicationCommandProperties> applicationCommandProperties = new();

            foreach (CommandInfo command in newOrChangedCommands)
            {
                SlashCommandBuilder builder = new SlashCommandBuilder();

                builder.WithName(command.Name);
                builder.WithDescription(command.Summary ?? "No description available");

                foreach (ParameterInfo parameter in command.Parameters)
                {
                    builder.AddOption(parameter.Name, parameter.Type,
                        parameter.Summary ?? parameter.Name, isRequired: !parameter.IsOptional);
                }

                applicationCommandProperties.Add(builder.Build());
            }

            try
            {
                await guild.BulkOverwriteApplicationCommandAsync(applicationCommandProperties.ToArray());
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }

            // Console.WriteLine("Available slash commands: \n" + string.Join(", ",
            // CommandMaster.CommandList.AsParallel().ToList().ConvertAll(c => c.Aliases[0].ToString())));
            CommandMaster.ExportCommands();
        }
    }

    public class CommandHandler
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;

        public CommandHandler(DiscordSocketClient client, CommandService commands)
        {
            this.client = client;
            this.commands = commands;
        }

        public async Task InstallCommandsAsync()
        {
            client.MessageReceived += HandleCommandAsync;
            client.SlashCommandExecuted += HandleSlashCommandAsync;
            client.ButtonExecuted += ApplicationModuleHelper.InteractionHandler;
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            // await commands.AddModuleAsync(typeof(GoogleModule), null);
        }

        private Task HandleCommandAsync(SocketMessage messageParam)
        {
            SocketUserMessage message = messageParam as SocketUserMessage;
            if (message == null) return Task.CompletedTask;

            int argPos = 0;

            // Console.WriteLine("Received: " + message.ToString());

            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return Task.CompletedTask;

            CommandMaster.CheckTextCommand(new SocketCommandContext(client, message));
            return Task.CompletedTask;
        }

        private async Task HandleSlashCommandAsync(SocketSlashCommand command)
        {
            try
            {
                _ = CommandMaster.Execute(command);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                try
                {
                    await command.ModifyOriginalResponseAsync(properties =>
                    {
                        properties.Content = "Something went wrong. Please try again.";
                    });
                }
                catch (Exception)
                {
                    //* if the command doesnt exist, it throws an exception -> ignore
                }
            }
        }
    }
}