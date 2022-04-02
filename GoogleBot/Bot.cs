using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using GoogleBot.Exceptions;
using GoogleBot.Interactions;
using Newtonsoft.Json;
using static GoogleBot.Util;
using CommandInfo = GoogleBot.Interactions.Commands.CommandInfo;
using ParameterInfo = GoogleBot.Interactions.Commands.ParameterInfo;


namespace GoogleBot;

internal class Bot
{
    private DiscordSocketClient client = null!;
    private CommandHandler commandHandler = null!;

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
        // InteractionMaster.InstantiateCommands();
        InteractionMaster.MountModules();

        Console.WriteLine("Client Ready");
        try
        {
            Console.WriteLine("---------");
            await RegisterGlobalCommandsAsync();
            Console.WriteLine("---------");
            await RegisterDevOnlyCommandsAsync();
            Console.WriteLine("---------");
            InteractionMaster.ExportCommands();
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


    /// <summary>
    /// Checks currently deployed global commands and compares them with the commands from the modules.
    /// If they changed -> override them
    /// </summary>
    private async Task RegisterGlobalCommandsAsync()
    {
        Console.WriteLine("Checking global commands");
        // List<CommandInfo> newOrChangedCommands = new List<CommandInfo>();
        bool changed = false;

        List<CommandInfo> newOrChangedCommands = new();
        List<CommandInfo> existingCommands = InteractionMaster.ImportAllCommands().FindAll(c => !c.IsDevOnly);
        List<CommandInfo> availableCommands = InteractionMaster.AllCommands.FindAll(c => !c.IsDevOnly);

        List<CommandInfo> availableSlashCommands = InteractionMaster.CommandList.FindAll(c => !c.IsDevOnly);
        List<CommandInfo> availableMessageCommands = InteractionMaster.MessageCommands.FindAll(c => !c.IsDevOnly);

        // Console.WriteLine(existingCommands.Count + " " + availableCommands.Count);

        foreach (CommandInfo command in availableCommands)
        {
            bool isNew = true;
            foreach (CommandInfo existingCommand in existingCommands)
            {
                if (command.Id.Equals(existingCommand.Id))
                {
                    isNew = false;
                    // Console.WriteLine("comparing " + command + " vs " + existingCommand);
                    if (!CommandsApproximatelyEqual(command, existingCommand))
                    {
                        // Console.WriteLine("CHANGED");
                        newOrChangedCommands.Add(command);
                        changed = true;
                        break;
                    }
                    // Console.WriteLine("NOT changed");
                }
            }

            changed |= isNew;

            if (isNew)
            {
                newOrChangedCommands.Add(command);
            }
        }

        changed |= availableCommands.Count != existingCommands.Count;


        if (!changed)
        {
            Console.WriteLine("No commands changed");
            return;
        }

        Console.WriteLine("New or changed global commands found:");
        Console.WriteLine(string.Join("\n", newOrChangedCommands));

        
        // var guild = client.GetGuild(guildId);

        List<ApplicationCommandProperties> applicationCommandProperties = new();

        //* Add slash commands
        foreach (CommandInfo command in availableSlashCommands)
        {
            SlashCommandBuilder builder = new SlashCommandBuilder();

            builder.WithName(command.Name);
            builder.WithDescription(command.Summary ?? "No description available");
            
            foreach (ParameterInfo parameter in command.Parameters)
            {
                var option = new SlashCommandOptionBuilder()
                    .WithName(parameter.Name)
                    .WithType(parameter.Type)
                    .WithDescription(parameter.Summary)
                    .WithRequired(!parameter.IsOptional);
                if (parameter.Choices.Length > 0)
                {
                    if (parameter.Type != ApplicationCommandOptionType.Integer)
                    {
                        throw new CommandParameterException(parameter, "Choice parameter must be type int!");
                    }
                    foreach ((int value, string name) in parameter.Choices)
                    {
                        option.AddChoice(name, value);
                    }
                }


                builder.AddOption(option);
            }

            if (command.IsOptionalEphemeral)
            {
                builder.AddOption("hidden", ApplicationCommandOptionType.Boolean,
                    "Whether the responds should be private", false);
            }

            applicationCommandProperties.Add(builder.Build());
        }

        //* Add message commands
        foreach (CommandInfo command in availableMessageCommands)
        {
            MessageCommandBuilder builder = new MessageCommandBuilder();

            builder.WithName(command.Name);
            applicationCommandProperties.Add(builder.Build());
        }

        try
        {
            await client.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());

            // await guild.BulkOverwriteApplicationCommandAsync(applicationCommandProperties.ToArray());
        }
        catch (HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception, Formatting.Indented);

            // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
            Console.WriteLine(json);
        }

        // Console.WriteLine("Available slash commands: \n" + string.Join(", ",
        // InteractionMaster.CommandList.AsParallel().ToList().ConvertAll(c => c.Aliases[0].ToString())));
    }

    /// <summary>
    /// Check currently deployed dev-only commands and compares them with the commands from the modules
    /// If they changed -> override
    /// </summary>
    private async Task RegisterDevOnlyCommandsAsync()
    {
        Console.WriteLine("Checking DevOnly commands");
        // List<CommandInfo> newOrChangedCommands = new List<CommandInfo>();
        bool changed = false;

        List<CommandInfo> newOrChangedCommands = new();
        List<CommandInfo> existingCommands = InteractionMaster.ImportSlashCommands().FindAll(c => c.IsDevOnly);
        List<CommandInfo> availableCommands = InteractionMaster.CommandList.FindAll(c => c.IsDevOnly);

        // Console.WriteLine(existingCommands.Count + " " + availableCommands.Count);

        foreach (CommandInfo command in availableCommands)
        {
            bool isNew = true;
            foreach (CommandInfo existingCommand in existingCommands)
            {
                if (command.Name.Equals(existingCommand.Name))
                {
                    isNew = false;
                    // Console.WriteLine("comparing " + command + " vs " + existingCommand);
                    if (!CommandsApproximatelyEqual(command, existingCommand))
                    {
                        newOrChangedCommands.Add(command);
                        changed = true;
                        break;
                    }
                }
            }

            changed |= isNew;

            if (isNew)
            {
                newOrChangedCommands.Add(command);
            }
        }

        changed |= availableCommands.Count != existingCommands.Count;


        if (!changed)
        {
            Console.WriteLine("No commands changed");
            return;
        }

        Console.WriteLine("New or changed dev-only commands:");
        Console.WriteLine(string.Join("\n", newOrChangedCommands));
        
        var guild = client.GetGuild(Secrets.DevGuildId);
        if (guild == null)
        {
            Console.WriteLine("Invalid dev guild");
            return;
        }

        List<ApplicationCommandProperties> applicationCommandProperties = new();

        foreach (CommandInfo command in availableCommands)
        {
            SlashCommandBuilder builder = new SlashCommandBuilder();

            builder.WithName(command.Name);
            builder.WithDescription(command.Summary ?? "No description available");

            foreach (ParameterInfo parameter in command.Parameters)
            {
                var option = new SlashCommandOptionBuilder()
                    .WithName(parameter.Name)
                    .WithType(parameter.Type)
                    .WithDescription(parameter.Summary)
                    .WithRequired(!parameter.IsOptional);
                if (parameter.Choices.Length > 0)
                {
                    if (parameter.Type != ApplicationCommandOptionType.Integer)
                    {
                        throw new CommandParameterException(parameter, "Choice parameter must be type int!");
                    }
                    foreach ((int value, string name) in parameter.Choices)
                    {
                        option.AddChoice(name, value);
                    }
                }


                builder.AddOption(option);
            }

            if (command.IsOptionalEphemeral)
            {
                builder.AddOption("hidden", ApplicationCommandOptionType.Boolean,
                    "Whether the responds should be private", false);
            }
            

            applicationCommandProperties.Add(builder.Build());
        }

        try
        {
            await guild.BulkOverwriteApplicationCommandAsync(applicationCommandProperties.ToArray());

            // await guild.BulkOverwriteApplicationCommandAsync(applicationCommandProperties.ToArray());
        }
        catch (HttpException exception)
        {
            var json = JsonConvert.SerializeObject(exception, Formatting.Indented);

            // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
            Console.WriteLine(json);
        }

        // Console.WriteLine("Available slash commands: \n" + string.Join(", ",
        // InteractionMaster.CommandList.AsParallel().ToList().ConvertAll(c => c.Aliases[0].ToString())));
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
        client.ButtonExecuted += HandleInteractionAsync;
        client.MessageCommandExecuted += HandleMessageCommandAsync;
        await commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        // await commands.AddModuleAsync(typeof(GoogleModule), null);
    }

    private Task HandleCommandAsync(SocketMessage messageParam)
    {
        SocketUserMessage? message = messageParam as SocketUserMessage;
        if (message == null) return Task.CompletedTask;

        int argPos = 0;

        // Console.WriteLine("Received: " + message.ToString());

        if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos)) ||
            message.Author.IsBot)
            return Task.CompletedTask;

        InteractionMaster.CheckTextCommand(new SocketCommandContext(client, message));
        return Task.CompletedTask;
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        try
        {
            _ = InteractionMaster.ExecuteSlashCommand(command);
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

    private Task HandleMessageCommandAsync(SocketMessageCommand command)
    {
        
        try
        {
            _ = InteractionMaster.ExecuteMessageCommand(command);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }

        return Task.CompletedTask;
    }

    private Task HandleInteractionAsync(SocketMessageComponent component)
    {
        _ = InteractionMaster.HandleInteraction(component);

        return Task.CompletedTask;
    }
}