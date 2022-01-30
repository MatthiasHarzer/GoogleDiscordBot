using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;
using GoogleBot.Interactions.Modules;
using PreconditionAttribute = GoogleBot.Interactions.CustomAttributes.PreconditionAttribute;

namespace GoogleBot.Interactions;

/// <summary>
/// Keeps track of commands and executes them
/// </summary>
public static class CommandMaster
{
    public static readonly List<CommandInfo> CommandList = new();

    public static readonly List<CommandInfo> MessageCommands = new();

    private static Dictionary<string, List<MethodInfo>> ComponentCallbacks { get; } = new();


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
    /// </summary>
    public static void MountModules()
    {
        //* Add regular commands
        IEnumerable<CommandModuleBase> commandModules = typeof(CommandModuleBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(CommandModuleBase)) && !t.IsAbstract)
            .Select(t => (CommandModuleBase)Activator.CreateInstance(t));

        foreach (CommandModuleBase module in commandModules)
        {
            // if (module != null) Helpers.Add(new ApplicationModuleHelper(module));
            if (module != null) AddSlashCommandModule(module);
        }

        //* Add message commands
        IEnumerable<MessageCommandsModuleBase> messageCommandModules = typeof(MessageCommandsModuleBase).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(MessageCommandsModuleBase)) && !t.IsAbstract)
            .Select(t => (MessageCommandsModuleBase)Activator.CreateInstance(t));

        foreach (MessageCommandsModuleBase module in messageCommandModules)
        {
            // if (module != null) MessageCommandHelpers.Add(new ApplicationModuleHelper(module));
            if (module != null) AddMessageCommandModule(module);
        }
    }


    /// <summary>
    /// Add a <see cref="CommandModuleBase"/> and its commands (slash commands)
    /// </summary>
    /// <param name="module">The module to add</param>
    private static void AddSlashCommandModule(CommandModuleBase module)
    {
        Type moduleType = module.GetType();
        // Console.WriteLine("app cmd  " + ModuleType.BaseType + " -> " + CommandModuleType);
        bool isDevOnlyModule = moduleType.GetCustomAttribute<DevOnlyAttribute>()?.IsDevOnly ?? false;

        //* Get all methods in the module
        foreach (MethodInfo method in moduleType.GetMethods())
        {
            CommandAttribute commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            SummaryAttribute summaryAttribute = method.GetCustomAttribute<SummaryAttribute>();
            // AliasAttribute aliasAttribute = method.GetCustomAttribute<AliasAttribute>();
            PrivateAttribute privateAttribute = method.GetCustomAttribute<PrivateAttribute>();
            LinkComponentInteractionAttribute linkComponentAttribute =
                method.GetCustomAttribute<LinkComponentInteractionAttribute>();
            PreconditionAttribute preconditionAttribute = method.GetCustomAttribute<PreconditionAttribute>();
            ParameterInfo[] parameterInfo = method.GetParameters().ToList().ConvertAll(p => new ParameterInfo
            {
                Summary = (p.GetCustomAttribute<SummaryAttribute>()?.Text ?? p.Name) ?? string.Empty,
                Type = p.GetCustomAttribute<OptionTypeAttribute>()?.Type ?? Util.ToOptionType(p.ParameterType),
                Name = p.GetCustomAttribute<NameAttribute>()?.Text ?? p.Name ?? string.Empty,
                IsMultiple = p.GetCustomAttribute<MultipleAttribute>()?.IsMultiple ?? false,
                IsOptional = p.HasDefaultValue,
            }).ToArray();


            bool devonly = isDevOnlyModule || (method.GetCustomAttribute<DevOnlyAttribute>()?.IsDevOnly ?? false);


            //* All methods must be async tasks
            if (method.ReturnType == typeof(Task))
            {
                if (commandAttribute != null)
                {
                    //* -> is command 
                    bool isEphemeral = privateAttribute?.IsPrivate != null && privateAttribute.IsPrivate;
                    bool overrideDefer = method.GetCustomAttribute<OverrideDeferAttribute>()?.DeferOverride ?? false;


                    if (!AddCommand(new CommandInfo
                        {
                            Name = commandAttribute.Text,
                            Summary = summaryAttribute?.Text ?? "No description available",
                            Type = CommandType.SlashCommand,
                            Parameters = parameterInfo,
                            Method = method,
                            IsPrivate = isEphemeral,
                            IsDevOnly = devonly,
                            OverrideDefer = overrideDefer,
                            IsOptionalEphemeral =
                                method.GetCustomAttribute<OptionalEphemeralAttribute>()?.IsOptionalEphemeral ?? false,
                            Preconditions = new Preconditions
                            {
                                RequiresMajority = preconditionAttribute?.RequiresMajority ??
                                                   new PreconditionAttribute().RequiresMajority,
                                MajorityVoteButtonText = preconditionAttribute?.ButtonText ??
                                                         new PreconditionAttribute().ButtonText,
                                RequiresBotConnected = preconditionAttribute?.RequiresBotConnected ??
                                                       new PreconditionAttribute().RequiresBotConnected,
                            }
                        }))
                    {
                        Console.WriteLine(
                            $"Slash Command {commandAttribute.Text} in {moduleType} already exists somewhere else! -> no new command was added");
                    }
                }
                else if (linkComponentAttribute != null)
                {
                    //* -> Is component interaction callback
                    string customId = linkComponentAttribute.CustomId;

                    if (ComponentCallbacks.Keys.Contains(customId))
                    {
                        ComponentCallbacks[customId].Add(method);
                    }
                    else
                    {
                        ComponentCallbacks.Add(customId, new List<MethodInfo> { method });
                    }
                }
            }
        }
    }


    /// <summary>
    /// Add a <see cref="MessageCommandsModuleBase"/> and its commands (message commands)
    /// </summary>
    /// <param name="module">The module to add</param>
    private static void AddMessageCommandModule(MessageCommandsModuleBase module)
    {
        Type moduleType = module.GetType();
        // Console.WriteLine("MCMD " + ModuleType.BaseType + " -> " + CommandModuleType);
        bool isDevOnlyModule = moduleType.GetCustomAttribute<DevOnlyAttribute>()?.IsDevOnly ?? false;

        //* Get all methods in the module
        foreach (MethodInfo method in moduleType.GetMethods())
        {
            CommandAttribute commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            LinkComponentInteractionAttribute linkComponentAttribute =
                method.GetCustomAttribute<LinkComponentInteractionAttribute>();
            PreconditionAttribute preconditionAttribute = method.GetCustomAttribute<PreconditionAttribute>();

            bool devonly = isDevOnlyModule || (method.GetCustomAttribute<DevOnlyAttribute>()?.IsDevOnly ?? false);

            //* All methods must be async tasks
            if (method.ReturnType == typeof(Task))
            {
                if (commandAttribute != null)
                {
                    //* -> is command 
                    if (!AddCommand(new CommandInfo
                        {
                            Name = commandAttribute.Text,
                            Type = CommandType.MessageCommand,
                            Method = method,
                            IsDevOnly = devonly,
                            Preconditions = new Preconditions
                            {
                                RequiresMajority = preconditionAttribute?.RequiresMajority ??
                                                   new PreconditionAttribute().RequiresMajority,
                                MajorityVoteButtonText = preconditionAttribute?.ButtonText ??
                                                         new PreconditionAttribute().ButtonText,
                                RequiresBotConnected = preconditionAttribute?.RequiresBotConnected ??
                                                       new PreconditionAttribute().RequiresBotConnected,
                            }
                        }))
                    {
                        Console.WriteLine(
                            $"Message Command {commandAttribute.Text} in {moduleType} already exists somewhere else! -> no new command was added");
                    }
                }
                else if (linkComponentAttribute != null)
                {
                    //* -> Is component interaction callback
                    string customId = linkComponentAttribute.CustomId;

                    if (ComponentCallbacks.ContainsKey(customId))
                    {
                        ComponentCallbacks[customId].Add(method);
                    }
                    else
                    {
                        ComponentCallbacks.Add(customId, new List<MethodInfo> { method });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Add a command to the commands list if it does not exist yet 
    /// </summary>
    /// <param name="commandInfo">The command to add</param>
    /// <returns>True if the command was added, false if it already exists</returns>
    private static bool AddCommand(CommandInfo commandInfo)
    {
        // Console.WriteLine($"trying to Add command {commandInfo.Name} in {ModuleType} where CommandModuleType = {CommandModuleType}");
        switch (commandInfo.Type)
        {
            case CommandType.MessageCommand:
                if (MessageCommands.FindAll(com => com.Name == commandInfo.Name).Count > 0) return false;
                //* The command does not exist yet -> add
                MessageCommands.Add(commandInfo);
                break;
            case CommandType.SlashCommand:
            case CommandType.UserCommand:
            default:
                if (CommandList.FindAll(com => com.Name == commandInfo.Name).Count != 0) return false;
                //* The command does not exist yet -> add
                CommandList.Add(commandInfo);
                break;
        }

        return true;
    }

    /// <summary>
    /// Execute a given slash command
    /// </summary>
    /// <param name="command">The command</param>
    public static async Task Execute(SocketSlashCommand command)
    {
        try
        {
            Context commandContext = new Context(command);
            CommandInfo commandInfo = commandContext.CommandInfo!;

            // if (commandContext.CommandInfo is not { OverrideDefer: true })
            // {
            //     commandContext.Command?.DeferAsync(ephemeral: commandInfo is { IsPrivate: true });
            // }

            if (commandInfo is { Method: not null })
            {
                ModuleBase module = commandInfo.GetNewModuleInstanceWith(commandContext);

                Console.WriteLine($"Found /{commandInfo?.Name} in {module}");

                object[] args = commandContext.Arguments;

                await command.DeferAsync(commandContext.IsEphemeral);

                PreconditionWatcher watcher = commandContext.GuildConfig.GetWatcher(commandInfo);
                bool preconditionsMet =
                    await watcher.CheckPreconditions(commandContext, module, args);
                if (!preconditionsMet)
                    return;


                // Console.WriteLine($"Executing with args: {string.Join(", ", args)}");


                // helper.SetContext(commandContext);
                try
                {
                    // Console.WriteLine(args.Length);
                    await ((Task)commandInfo.Method!.Invoke(module, args))!;
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

    /// <summary>
    /// Execute an <see cref="SocketMessageCommand"/>
    /// </summary>
    /// <param name="command">The command to execute</param>
    public static async Task ExecuteMessageCommand(SocketMessageCommand command)
    {
        Context commandContext = new Context(command);
        CommandInfo commandInfo = commandContext.CommandInfo!;

        await command.DeferAsync();


        if (commandInfo is { Method: not null })
        {
            ModuleBase module = commandInfo.GetNewModuleInstanceWith(commandContext);
            Console.WriteLine($"Found message command {commandInfo?.Name} in {module}");
            object[] args = commandContext.Arguments;

            // Console.WriteLine(string.Join(", ", args));
            // Console.WriteLine(commandInfo);


            PreconditionWatcher watcher = commandContext.GuildConfig.GetWatcher(commandInfo);
            bool preconditionsMet =
                await watcher.CheckPreconditions(commandContext, module, args);
            // Console.WriteLine(preconditionsMet);
            if (!preconditionsMet)
                return;

            // Console.WriteLine(string.Join(", ", args));
            // Console.WriteLine(commandInfo);
            try
            {
                // var module = (ModuleBase) Activator.CreateInstance(commandInfo.Method.Module.GetType());
                // Console.WriteLine(module + " " + commandInfo.NewModule);


                await ((Task)commandInfo.Method!.Invoke(module, args))!;
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
    /// Handle an interaction of a <see cref="SocketMessageComponent"/>
    /// </summary>
    /// <param name="component">The interaction component</param>
    public static async Task HandleInteraction(SocketMessageComponent component)
    {
        foreach (KeyValuePair<string, List<MethodInfo>> componentCallback in ComponentCallbacks)
        {
            //* Key = the components custom id or * for any id
            //* Value = List of methods to call when the custom id appears

            bool startsWith =
                componentCallback.Key.Length > 1 &&
                componentCallback.Key.Last() == '*'; //* Match bla-id-* to bla-id-123
            bool endsWith =
                componentCallback.Key.Length > 1 &&
                componentCallback.Key.First() == '*'; // Match *-bla-id to 123-bla-id

            string key = componentCallback.Key;

            if (startsWith || endsWith)
            {
                key = componentCallback.Key.Replace("*", "");
            }

            if (componentCallback.Key == component.Data.CustomId
                || componentCallback.Key == "*"
                || (startsWith && component.Data.CustomId.StartsWith(key))
                || (endsWith && component.Data.CustomId.EndsWith(key)))
            {
                // Console.WriteLine($"FOUND {string.Join(", ", componentCallback.Value.ConvertAll(m=>m.Name))}");
                foreach (MethodInfo method in componentCallback.Value)
                {
                    var module = (ModuleBase)Activator.CreateInstance(method.DeclaringType!);
                    module!.Context = new Context(component);
                    await (Task)method.Invoke(module, new object[] { component })!;
                }
            }
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


        if (command.IsValid)
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