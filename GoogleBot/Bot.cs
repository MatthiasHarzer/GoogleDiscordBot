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
            CommandMaster.AddApplicationCommands();

            RegisterCommandsAsync();

            // Console.WriteLine(string.Join(", ", CommandHandler._coms.Commands.ToList().ConvertAll(c=>c.Name)));

            // _ = InitSlashCommandsAsync();
            // Console.WriteLine(string.Join(", ",CommandHandler._coms.Commands.AsParallel().ToList().ConvertAll(c=>String.Join(" / ", c.Aliases))));
        }

        private async Task InitSlashCommandsAsync()
        {
            List<ApplicationCommandProperties> applicationCommandProperties = new();
            foreach (CommandInfo command in CommandMaster.LegacyCommandsList)
            {
                SlashCommandBuilder builder = new SlashCommandBuilder();

                builder.WithName(command.Aliases[0]);
                builder.WithDescription(command.Summary ?? "No description available");

                foreach (ParameterInfo parameter in command.Parameters)
                {
                    builder.AddOption(parameter.Summary ?? parameter.Name, ApplicationCommandOptionType.String,
                        parameter.Summary ?? parameter.Name, isRequired: !parameter.IsOptional);
                }

                applicationCommandProperties.Add(builder.Build());
            }

            try
            {
                await client.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }

            Console.WriteLine("Available slash commands: \n" + string.Join(", ",
                CommandMaster.LegacyCommandsList.AsParallel().ToList().ConvertAll(c => c.Aliases[0].ToString())));
        }

        private async Task RegisterCommandsAsync()
        {
            ulong guildId = Secrets.DevGuildID;
            var guild = client.GetGuild(guildId);
            
            List<ApplicationCommandProperties> applicationCommandProperties = new();
            
            foreach (CommandInfo command in CommandMaster.CommandsList)
            {
                SlashCommandBuilder builder = new SlashCommandBuilder();

                builder.WithName(command.Name);
                builder.WithDescription(command.Summary ?? "No description available");

                foreach (ParameterInfo parameter in command.Parameters)
                {
                    builder.AddOption(parameter.Name, ToApplicationCommandOptionType(parameter.Type),
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

            Console.WriteLine("Available slash commands: \n" + string.Join(", ",
                CommandMaster.CommandsList.AsParallel().ToList().ConvertAll(c => c.Aliases[0].ToString())));
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

            // SocketCommandContext context = new SocketCommandContext(client, message);


            var _ = ExecuteCommandAsync(message);
            return Task.CompletedTask;


            // await commands.ExecuteAsync(
            //     context: context,
            //     argPos: argPos,
            //     services: null);
        }

        private async Task ExecuteCommandAsync(SocketUserMessage message)
        {
            IDisposable typing = null;
            SocketCommandContext socketCommandContext = new SocketCommandContext(client, message);

            // Just to be sure, the typing will be dismissed, try catch
            try
            {
                CommandConversionInfo convertedCommandInfo = GetCommandInfoFromMessage(socketCommandContext.Message);


                EmbedBuilder embed = new EmbedBuilder();


                CommandReturnValue returnValue = new CommandReturnValue();


                switch (convertedCommandInfo.State)
                {
                    case CommandConversionState.Success:
                        ExecuteContext executeContext =
                            new ExecuteContext(convertedCommandInfo.Command, socketCommandContext);

                        if (!convertedCommandInfo.Command.IsPrivate)
                            typing = executeContext.Channel.EnterTypingState();
                        returnValue =
                            await CommandMaster.LegacyExecute(executeContext, convertedCommandInfo.Arguments.ToArray());


                        break;
                    case CommandConversionState.Failed:
                        // embed = new EmbedBuilder().WithTitle("")
                        return;
                    case CommandConversionState.MissingArg:

                        embed.AddField("Missing args",
                            string.Join(" ",
                                convertedCommandInfo.MissingArgs.ToList().ConvertAll(a => $"<{a.Summary ?? a.Name}>")));
                        break;
                    case CommandConversionState.InvalidArgType:

                        embed.AddField("Invalid argument type provided",
                            string.Join("\n",
                                convertedCommandInfo.TargetTypeParam.ToList()
                                    .ConvertAll(tp => $"`{tp.Item1}` should be from type `{tp.Item2}`")));
                        break;
                    case CommandConversionState.SlashCommandExecutedAsTextCommand:
                        embed = null;
                        returnValue.WithText(
                            $"This command is slash-only! Please use `/{convertedCommandInfo.Command?.Name} {string.Join(" ", convertedCommandInfo.Command?.Parameters.ToList().ConvertAll(param => $"<{param.Summary ?? param.Name}>")!)}`");
                        break;
                }

                if (embed != null) returnValue.WithEmbed(embed);


                if (convertedCommandInfo.Command?.IsPrivate == true && convertedCommandInfo.State !=
                    CommandConversionState.SlashCommandExecutedAsTextCommand)
                {
                    await message.Author.SendMessageAsync(returnValue.Message, embed: returnValue.Embed?.Build(),
                        components: returnValue.Components?.Build()); // Reply in DMs 
                    await message.ReplyAsync("Replied in DMs"); // Reply in channel
                }
                else
                    await message.ReplyAsync(returnValue.Message, embed: returnValue.Embed?.Build(),
                        components: returnValue.Components?.Build());

                // else if (textMessage != null)
                // {
                //     if (context.Command.IsPrivate)
                //         await message.Author.SendMessageAsync(textMessage);
                //     else
                //         await message.ReplyAsync(textMessage);
                // }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            typing?.Dispose();
        }

        private async Task HandleSlashCommandAsync(SocketSlashCommand command)
        {
            try
            {
                // Console.WriteLine("command.Data.Name " + command.Data.Name);

                // command.

                // Console.WriteLine(command.CommandName);

                // ExecuteSlashCommandAsync(command);
                _ = CommandMaster.ExecuteAsync(command);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                await command.ModifyOriginalResponseAsync(properties =>
                {
                    properties.Content = $"Something went wrong ({e.Message})";
                });
            }

            // SocketMessage m =  (client, command.Data.Id, command.Channel, command.User, MessageSource.User);


            // switch (command.Data.Name.ToString())
            // {
            //     case "play":
            //         // Console.WriteLine("PLAY");
            //         HandleSlashPlayCommand(command, channel);
            //         break;
            //     case "queue":
            //         embed = CommandExecutor.Queue(guildId);
            //         break;
            //     case "skip":
            //         embed = CommandExecutor.Skip(guildId);
            //         break;
            //     case "stop":
            //         embed = CommandExecutor.Stop(guildId);
            //         break;
            //     case "help":
            //         embed = CommandExecutor.Help();
            //         break;
            // }
        }

        private async void ExecuteSlashCommandAsync(SocketSlashCommand command)
        {
            try
            {
                CommandConversionInfo convertedCommandInfo = GetCommandInfoFromSlashCommand(command);


                await command.DeferAsync(convertedCommandInfo.Command?.IsPrivate == true);

                Console.WriteLine(convertedCommandInfo.Command.Name + " " + convertedCommandInfo.State);


                // Console.WriteLine(string.Join(", " , command.Data.Options.ToList().ConvertAll(option=>option.Value)));
                switch (convertedCommandInfo.State)
                {
                    case CommandConversionState.NotFound:
                        await command.ModifyOriginalResponseAsync(properties =>
                            properties.Content = "Looks like this command does not exist...");
                        break;

                    case CommandConversionState.Success:
                        using (CommandReturnValue returnValue = await CommandMaster.LegacyExecute(new ExecuteContext(command),
                                   convertedCommandInfo.Arguments.ToArray()))
                        {
                            await command.ModifyOriginalResponseAsync(properties =>
                            {
                                properties.Embed = returnValue.Embed?.Build();
                                properties.Components = returnValue.Components?.Build();
                                properties.Content = returnValue.Message;
                            });
                        }


                        break;
                }


                // Console.WriteLine(embed);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Source);
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