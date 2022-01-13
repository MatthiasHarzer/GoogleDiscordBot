using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static GoogleBot.Util;
using GoogleBot.Interactions;


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

            await client.SetGameAsync("with Google", type:ActivityType.Playing);

            // Commands.testing();
            CommandMaster.InstantiateCommands();
            
            // Console.WriteLine(string.Join(", ", CommandHandler._coms.Commands.ToList().ConvertAll(c=>c.Name)));

            //InitSlashCommandsAsync();
            // Console.WriteLine(string.Join(", ",CommandHandler._coms.Commands.AsParallel().ToList().ConvertAll(c=>String.Join(" / ", c.Aliases))));
        }

        // private async Task InitSlashCommandsAsync()
        // {
        //     List<ApplicationCommandProperties> applicationCommandProperties = new();
        //     foreach (CommandInfo command in CommandHandler._coms.Commands)  
        //     {
        //         SlashCommandBuilder builder = new SlashCommandBuilder();
        //     
        //         builder.WithName(command.Aliases[0]);
        //         builder.WithDescription(command.Summary ?? "No description available");
        //         if (command.Parameters.Count > 0)
        //         {
        //             foreach (ParameterInfo parameter in command.Parameters)
        //             {
        //                 builder.AddOption(parameter.Summary ?? parameter.Name, ApplicationCommandOptionType.String,
        //                     parameter.Summary ?? parameter.Name, isRequired: !parameter.IsOptional);
        //             }
        //         }
        //         applicationCommandProperties.Add(builder.Build());
        //         
        //         
        //     }
        //     try
        //     {
        //         await client.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());
        //     }
        //     catch(ApplicationCommandException exception)
        //     {
        //         var json = JsonConvert.SerializeObject(exception, Formatting.Indented);
        //         
        //         // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
        //         Console.WriteLine(json);
        //     }
        //     // Console.WriteLine("Available slash commands: \n" + string.Join(", ",CommandHandler._coms.Commands.AsParallel().ToList().ConvertAll(c=>c.Aliases[0].ToString())));
        // }
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
            var (context, info) = ExecuteContext.From(new SocketCommandContext(client, message));

            EmbedBuilder embed = new EmbedBuilder();
            string m = null;
            bool isEmbed = false;

            IDisposable typing = null;
   
            
            switch (info.State)
            {
                case CommandConversionState.Success:
                    typing = context.Channel?.EnterTypingState();
                    CommandReturnValue retval = await CommandMaster.Execute(context, info.Arguments.ToArray());
                    if (retval == null)
                    {
                        isEmbed = true;
                        embed = new EmbedBuilder().WithTitle("Something went wrong.");
                    }
                    else
                    {
                        isEmbed = retval.IsEmbed;
                        embed = retval.Embed;
                        m = retval.Message;
                    }
                    
                    break;
                case CommandConversionState.Failed:
                    // embed = new EmbedBuilder().WithTitle("")
                    return;
                case CommandConversionState.MissingArg:
                    isEmbed = true;
                    embed.AddField("Missing args",
                        string.Join(" ", info.MissingArgs.ToList().ConvertAll(a => $"<{a.Summary ?? a.Name}>")));
                    break;
                case CommandConversionState.InvalidArgType:
                    isEmbed = true;
                    embed.AddField("Invalid argument type provided",
                        string.Join("\n",
                            info.TargetTypeParam.ToList()
                                .ConvertAll(tp => $"`{tp.Item1}` should be from type `{tp.Item2}`")));
                    break;
            }

            if (isEmbed)
            {
                await message.ReplyAsync(embed: embed.Build());
            }
            else if(m != null)
            {
                await message.ReplyAsync(m);
            }
            typing?.Dispose();
        }

        private async Task HandleSlashCommandAsync(SocketSlashCommand command)
        {
            try
            {

                // Console.WriteLine("command.Data.Name " + command.Data.Name);
                await command.DeferAsync(true);

                // Console.WriteLine(command.CommandName);

                ExecuteSlashCommandAsync(command);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
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
                CommandConversionInfo info = GetCommandInfoFromSlashCommand(command);
                
                // Console.WriteLine(string.Join(", " , command.Data.Options.ToList().ConvertAll(option=>option.Value)));
                switch (info.State)
                {
                    case CommandConversionState.NotFound:
                        await command.ModifyOriginalResponseAsync(properties => properties.Content = "Looks like this command does not exist...");
                        break;
                    
                    case CommandConversionState.Success:
                        CommandReturnValue retval = await CommandMaster.Execute(new ExecuteContext(command), info.Arguments.ToArray());
                        if (retval.IsEmbed)
                        {
                    
                            await command.ModifyOriginalResponseAsync(properties => { properties.Embed = retval.Embed.Build(); });
                        }
                        else
                        {
                            await command.ModifyOriginalResponseAsync(properties => properties.Content = retval.Message);
                        }
                        break;
                }
                
                
                    
                // Console.WriteLine(embed);

                
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Source);
                await command.ModifyOriginalResponseAsync(properties =>
                {
                    properties.Content = "Something went wrong. Please try again.";
                });
            }
            
        }
        
    }
}