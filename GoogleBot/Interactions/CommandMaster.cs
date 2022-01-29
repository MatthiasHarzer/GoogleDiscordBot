using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.Modules;

namespace GoogleBot.Interactions;

/// <summary>
/// Keeps track of commands and executes them
/// </summary>
public static class CommandMaster
{
    public static readonly List<CommandInfo> CommandList = new();
    public static readonly List<ApplicationModuleHelper> Helpers = new();

    public static readonly List<CommandInfo> MessageCommands = new();
    public static readonly List<ApplicationModuleHelper> MessageCommandHelpers = new();


    public static List<CommandInfo> AllCommands
    {
        get
        {
            List<CommandInfo> cmds = new List<CommandInfo>(CommandList);
            cmds.AddRange(MessageCommands);
            return cmds;
        }
    }

    /// <summary>
    /// Gets the application command with the given name
    /// </summary>
    /// <param name="commandName"></param>
    /// <returns>The command or null if not found</returns>
    public static CommandInfo GetCommandFromName(string commandName)
    {
        var res = CommandList.FindAll(c => c.Name == commandName);
        if (res.Count > 0)
        {
            return res.First();
        }

        return null;
    }

    /// <summary>
    /// Gets the message command with the given name
    /// </summary>
    /// <param name="messageCommandName">The commands name</param>
    /// <returns>The message command info</returns>
    public static CommandInfo GetMessageCommandFromName(string messageCommandName)
    {
        var res = MessageCommands.FindAll(c => c.Name == messageCommandName);
        if (res.Count > 0)
        {
            return res.First();
        }

        return null;
    }

    /// <summary>
    /// Get all modules with base class <see cref="CommandModuleBase"/> for SlashCommands or <see cref="MessageCommandsModuleBase"/> for message commands
    /// and create their <see cref="ApplicationModuleHelper"/> 
    /// </summary>
    public static void MountModules()
    {
        //* Add regular commands
        IEnumerable<CommandModuleBase> commandModules = typeof(CommandModuleBase).Assembly.GetTypes()
            .Where(t =>t.IsSubclassOf(typeof(CommandModuleBase)) && !t.IsAbstract)
            .Select(t => (CommandModuleBase)Activator.CreateInstance(t));

        foreach (CommandModuleBase module in commandModules)
        {
            if (module != null) Helpers.Add(new ApplicationModuleHelper(module));
        }

        //* Add message commands
        IEnumerable<MessageCommandsModuleBase> messageCommandModules = typeof(MessageCommandsModuleBase).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(MessageCommandsModuleBase)) && !t.IsAbstract)
            .Select(t => (MessageCommandsModuleBase)Activator.CreateInstance(t));

        foreach (MessageCommandsModuleBase module in messageCommandModules)
        {
            if (module != null) MessageCommandHelpers.Add(new ApplicationModuleHelper(module));
        }
    }

    /// <summary>
    /// Execute a given slash command
    /// </summary>
    /// <param name="command">The command</param>
    public static async Task Execute(SocketSlashCommand command)
    {
        try
        {
            ApplicationModuleHelper helper =
                Helpers.Find(helper => helper.GetCommandsAsText().Contains(command.CommandName))!;
            Context commandContext = new Context(command);
            CommandInfo commandInfo = commandContext.CommandInfo!;


            Console.WriteLine($"Found /{commandInfo?.Name} in {helper?.ModuleType}");

            // if (commandContext.CommandInfo is not { OverrideDefer: true })
            // {
            //     commandContext.Command?.DeferAsync(ephemeral: commandInfo is { IsPrivate: true });
            // }

            if (helper != null && commandInfo is { Method: not null })
            {
                object[] args = commandContext.Arguments;

                await command.DeferAsync(commandContext.IsEphemeral);

                PreconditionWatcher watcher = commandContext.GuildConfig.GetWatcher(commandInfo);
                bool preconditionsMet =
                    await watcher.CheckPreconditions(commandContext, helper.GetModuleInstance(commandContext), args);
                if (!preconditionsMet)
                    return;


                // Console.WriteLine($"Executing with args: {string.Join(", ", args)}");


                // helper.SetContext(commandContext);
                try
                {
                    var module = helper.GetModuleInstance(commandContext);
                    // Console.WriteLine(args.Length);
                    await ((Task)commandInfo.Method.Invoke(module, args))!;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
            else
            {
                commandContext.Command?.ModifyOriginalResponseAsync(properties =>
                {
                    properties.Content = "`Looks like that command doesn't exist. Sorry :/`";
                });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }

    public static async Task ExecuteMessageCommand(SocketMessageCommand command)
    {
        ApplicationModuleHelper helper =
            MessageCommandHelpers.Find(helper => helper.GetCommandsAsText().Contains(command.CommandName))!;
        Context commandContext = new Context(command);
        CommandInfo commandInfo = commandContext.CommandInfo!;

        await command.DeferAsync();

        Console.WriteLine($"Found message command {commandInfo?.Name} in {helper?.ModuleType}");

        if (helper != null && commandInfo is { Method: not null })
        {
            object[] args = commandContext.Arguments;

            // Console.WriteLine(string.Join(", ", args));
            // Console.WriteLine(commandInfo);
            
            PreconditionWatcher watcher = commandContext.GuildConfig.GetWatcher(commandInfo);
            bool preconditionsMet =
                await watcher.CheckPreconditions(commandContext, helper.GetModuleInstance(commandContext), args);
            // Console.WriteLine(preconditionsMet);
            if (!preconditionsMet)
                return;

            // Console.WriteLine(string.Join(", ", args));
            // Console.WriteLine(commandInfo);
            try
            {
                var module = helper.GetModuleInstance(commandContext);
                Console.WriteLine(module);
                await ((Task)commandInfo.Method.Invoke(module, args))!;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
        else
        {
            command?.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = "`Looks like that command doesn't exist. Sorry :/`";
            });
        }
    }

    /// <summary>
    /// Check if the message is a text command and reply with an message to use the application command
    /// </summary>
    /// <param name="socketCommandContext">The command context</param>
    public static void CheckTextCommand(SocketCommandContext socketCommandContext)
    {
        CommandInfo command = Util.GetTextCommandFromMessage(socketCommandContext.Message);

        IDisposable typing = null;

        ApplicationModuleHelper helper = Helpers.Find(helper => helper.GetCommandsAsText().Contains(command.Name));

        if (helper != null)
        {
            typing = socketCommandContext.Channel.EnterTypingState();

            FormattedMessage message = Responses.DeprecationHint(command);
            socketCommandContext.Message?.ReplyAsync(message.Message, embed: message.Embed?.Build());
        }

        typing?.Dispose();
    }


    /// <summary>
    /// Convert all commands into json and save them in a file
    /// </summary>
    public static void ExportCommands()
    {
        JsonObject savable = new JsonObject();
        JsonArray commands = new JsonArray();
        JsonArray messageCommands = new JsonArray();

        //* Get all regular commands
        foreach (CommandInfo cmd in CommandList)
        {
            commands.Add(cmd.ToJson());
        }

        //* Get all message commands
        foreach (CommandInfo cmd in MessageCommands)
        {
            messageCommands.Add(cmd.ToJson());
        }

        savable.Add("commands", commands);
        savable.Add("messageCommands", messageCommands);
        // Console.WriteLine(JsonSerializer.Serialize(savable));

        File.WriteAllText("./commands.json", JsonSerializer.Serialize(savable));
    }


    /// <summary>
    /// Read local file and try converting the json to valid command infos
    /// </summary>
    /// <returns>The list of commands in the safe-file</returns>
    public static List<CommandInfo> ImportCommands()
    {
        List<CommandInfo> commandInfos = new();

        try
        {
            string content = File.ReadAllText("./commands.json");

            JsonObject json = JsonSerializer.Deserialize<JsonObject>(content);


            JsonArray jsonCommands = null;

            if (json != null && json.TryGetPropertyValue("commands", out var jn))
            {
                if (jn != null) jsonCommands = jn.AsArray();
            }

            if (jsonCommands != null)
            {
                foreach (JsonNode jsonCommand in jsonCommands)
                {
                    if (jsonCommand == null) continue;
                    try
                    {
                        commandInfos.Add(new CommandInfo().FromJson((JsonObject)jsonCommand));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                        // ignored
                    }
                }
            }
        }

        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            // -> something's fishy with the file or json
        }

        return commandInfos;
    }

    /// <summary>
    /// Read local file and try converting the json to valid message command infos
    /// </summary>
    /// <returns>The list of message commands in the safe-file</returns>
    public static List<CommandInfo> ImportMessageCommands()
    {
        List<CommandInfo> messageCommands = new();

        try
        {
            string content = File.ReadAllText("./commands.json");

            JsonObject json = JsonSerializer.Deserialize<JsonObject>(content);

            JsonArray jsonMessageCommands = null;
            if (json != null && json.TryGetPropertyValue("messageCommands", out var jmcmd))
            {
                jsonMessageCommands = jmcmd?.AsArray();
            }

            if (jsonMessageCommands != null)
            {
                foreach (JsonNode jsonMsgCommand in jsonMessageCommands)
                {
                    if (jsonMsgCommand == null) continue;
                    try
                    {
                        messageCommands.Add(new CommandInfo().FromJson((JsonObject)jsonMsgCommand));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                        // ignored
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            // -> something's fishy with the file or json
        }

        return messageCommands;
    }

    /// <summary>
    /// Gets all command from local file, including slash-, message- and user-commands (user commands not implemented yet)
    /// </summary>
    /// <returns>The list of commands</returns>
    public static List<CommandInfo> ImportAllCommands()
    {
        List<CommandInfo> cmds = ImportCommands();
        cmds.AddRange(ImportMessageCommands());
        return cmds;
    }
}