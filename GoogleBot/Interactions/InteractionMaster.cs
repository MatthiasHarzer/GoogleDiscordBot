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
using GoogleBot.Interactions.Commands;
using GoogleBot.Interactions.Context;
using GoogleBot.Interactions.CustomAttributes;
using GoogleBot.Interactions.Modules;
using GoogleBot.Interactions.Preconditions.Exceptions;
using CommandInfo = GoogleBot.Interactions.Commands.CommandInfo;
using ICommandContext = GoogleBot.Interactions.Context.ICommandContext;
using IModuleBase = GoogleBot.Interactions.Modules.IModuleBase;
using ModuleBase = GoogleBot.Interactions.Modules.ModuleBase;
using ParameterInfo = GoogleBot.Interactions.Commands.ParameterInfo;
using PreconditionAttribute = GoogleBot.Interactions.Preconditions.PreconditionAttribute;

namespace GoogleBot.Interactions;

/// <summary>
/// Keeps track of commands and executes them
/// </summary>
public static class InteractionMaster
{
    public static readonly List<CommandInfo> CommandList = new();

    public static readonly List<CommandInfo> MessageCommands = new();

    private static Dictionary<string, List<MethodInfo>> ComponentCallbacks { get; } = new();

    // private static Dictionary<string, ButtonOnClickCallback> AdditionalComponentCallbacks { get; } =
    //     new Dictionary<string, ButtonOnClickCallback>();


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
    public static CommandInfo? GetCommandFromName(string commandName)
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
    public static CommandInfo? GetMessageCommandFromName(string messageCommandName)
    {
        var res = MessageCommands.FindAll(c => c.Name == messageCommandName);
        if (res.Count > 0)
        {
            return res.First();
        }

        return null;
    }

    /// <summary>
    /// Get all modules with their commands
    /// <seealso cref="SlashCommandModuleBase"/>
    /// <seealso cref="MessageCommandModuleBase"/>
    /// <seealso cref="InteractionModuleBase"/>
    /// </summary>
    public static void MountModules()
    {
        //* Add regular commands
        IEnumerable<SlashCommandModuleBase?> slashCommandModules = typeof(SlashCommandModuleBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(SlashCommandModuleBase)) && !t.IsAbstract)
            .Select(t => (SlashCommandModuleBase)Activator.CreateInstance(t)!);

        foreach (SlashCommandModuleBase? module in slashCommandModules)
        {
            // if (module != null) Helpers.Add(new ApplicationModuleHelper(module));
            if (module != null)
                AddCommandModule(module);
        }

        //* Add message commands
        IEnumerable<MessageCommandModuleBase?> messageCommandModules = typeof(MessageCommandModuleBase).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(MessageCommandModuleBase)) && !t.IsAbstract)
            .Select(t => (MessageCommandModuleBase)Activator.CreateInstance(t)!);

        foreach (MessageCommandModuleBase? module in messageCommandModules)
        {
            // if (module != null) MessageCommandHelpers.Add(new ApplicationModuleHelper(module));
            if (module != null) AddMessageCommandModule(module);
        }

        //* Add Interaction Modules
        IEnumerable<InteractionModuleBase?> interactionModules = typeof(InteractionModuleBase).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(InteractionModuleBase)) && !t.IsAbstract)
            .Select(t => (InteractionModuleBase)Activator.CreateInstance(t)!);

        foreach (InteractionModuleBase? module in interactionModules)
        {
            // if (module != null) MessageCommandHelpers.Add(new ApplicationModuleHelper(module));
            if (module != null) AddInteractionModule(module);
        }
    }


    /// <summary>
    /// Add a <see cref="SlashCommandModuleBase"/> and its slash commands 
    /// </summary>
    /// <param name="module">The module to add</param>
    private static void AddCommandModule(SlashCommandModuleBase module)
    {
        Type moduleType = module.GetType();
        // Console.WriteLine("app cmd  " + ModuleType.BaseType + " -> " + CommandModuleType);
        bool isDevOnlyModule = moduleType.GetCustomAttribute<DevOnlyAttribute>()?.IsDevOnly ?? false;


        //* Get all methods in the module
        foreach (MethodInfo method in moduleType.GetMethods())
        {
            //* All methods must be async tasks
            CommandAttribute? commandAttribute = method.GetCustomAttribute<CommandAttribute>();

            if (method.ReturnType != typeof(Task)) continue;
            if (commandAttribute == null) continue;


            SummaryAttribute? summaryAttribute = method.GetCustomAttribute<SummaryAttribute>();
            // AliasAttribute aliasAttribute = method.GetCustomAttribute<AliasAttribute>();
            PrivateAttribute? privateAttribute = method.GetCustomAttribute<PrivateAttribute>();


            ParameterInfo[] parameterInfo = method.GetParameters().ToList().ConvertAll(p =>
            {
                Type? underlying = Nullable.GetUnderlyingType(p.ParameterType);
                bool nullable = underlying != null;
                ApplicationCommandOptionType pType = p.GetCustomAttribute<OptionTypeAttribute>()?.Type
                                                     ?? Util.ToOptionType(nullable ? underlying! : p.ParameterType);
                return new ParameterInfo
                {
                    Summary = (p.GetCustomAttribute<SummaryAttribute>()?.Text ?? p.Name) ?? string.Empty,
                    Type = pType,
                    Name = p.GetCustomAttribute<NameAttribute>()?.Text ?? p.Name ?? string.Empty,
                    IsMultiple = p.GetCustomAttribute<MultipleAttribute>()?.IsMultiple ?? false,
                    IsOptional = nullable || p.HasDefaultValue,
                    DefaultValue = nullable ? null : p.DefaultValue
                };
            }).ToArray();

            AutoDeleteOldComponentsAttribute? oldComponentsAttribute =
                method.GetCustomAttribute<AutoDeleteOldComponentsAttribute>();
            VoteConfigAttribute voteConfig =
                method.GetCustomAttribute<VoteConfigAttribute>() ?? new VoteConfigAttribute();
            // PreconditionAttribute? preconditionAttribute = method.GetCustomAttribute<PreconditionAttribute>();


            bool devonly = isDevOnlyModule || (method.GetCustomAttribute<DevOnlyAttribute>()?.IsDevOnly ?? false);


            //* -> is command 
            bool isEphemeral = privateAttribute?.IsPrivate != null && privateAttribute.IsPrivate;
            bool overrideDefer = method.GetCustomAttribute<OverrideDeferAttribute>()?.DeferOverride ?? false;

            // Precondition[] preconditions = (preconditionAttribute != null
            //     ? preconditionAttribute.PreconditionModules
            //         .ToList().ConvertAll(p => (Precondition)Activator.CreateInstance(p)!).ToArray()
            //     : Array.Empty<Precondition>())!;
            IEnumerable<PreconditionAttribute> preconditions = method.GetCustomAttributes().Select(a => a.GetType())
                .Where(t =>
                    t.IsSubclassOf(typeof(PreconditionAttribute)) && !t.IsAbstract)
                .Select(t => (PreconditionAttribute)Activator.CreateInstance(t)!);


            // IEnumerable<PreconditionAttribute> preconditions = typeof(PreconditionAttribute).Assembly.GetTypes()
            //     .Where(t => t.IsSubclassOf(typeof(PreconditionAttribute)) && !t.IsAbstract )
            //     .Select(t => (PreconditionAttribute)Activator.CreateInstance(t)!);

            if (!AddCommand(new CommandInfo
                {
                    Name = commandAttribute.Text,
                    Summary = summaryAttribute?.Text ?? "No description available",
                    Type = CommandType.SlashCommand,
                    Parameters = parameterInfo,
                    Method = method,
                    IsPrivate = isEphemeral,
                    IsDevOnly = devonly,
                    AutoDeleteOldComponents = oldComponentsAttribute?.AutoDelete ?? false,
                    OverrideDefer = overrideDefer,
                    IsOptionalEphemeral =
                        method.GetCustomAttribute<OptionalEphemeralAttribute>()?.IsOptionalEphemeral ?? false,
                    Preconditions = preconditions.ToArray(),
                    VoteConfig = voteConfig,
                }))
            {
                Console.WriteLine(
                    $"Slash Command {commandAttribute.Text} in {moduleType} already exists somewhere else! -> no new command was added");
            }
        }
    }


    /// <summary>
    /// Add a <see cref="SlashCommandModuleBase"/> and its message commands
    /// </summary>
    /// <param name="module">The module to add</param>
    private static void AddMessageCommandModule(MessageCommandModuleBase module)
    {
        Type moduleType = module.GetType();
        bool isDevOnlyModule = moduleType.GetCustomAttribute<DevOnlyAttribute>()?.IsDevOnly ?? false;

        //* Get all methods in the module
        foreach (MethodInfo method in moduleType.GetMethods())
        {
            CommandAttribute? commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            // LinkComponentInteractionAttribute? linkComponentAttribute =
            //     method.GetCustomAttribute<LinkComponentInteractionAttribute>();
            AutoDeleteOldComponentsAttribute? oldComponentsAttribute =
                method.GetCustomAttribute<AutoDeleteOldComponentsAttribute>();
            bool devonly = isDevOnlyModule || (method.GetCustomAttribute<DevOnlyAttribute>()?.IsDevOnly ?? false);

            //* All methods must be async tasks
            if (method.ReturnType != typeof(Task)) continue;
            if (commandAttribute == null) continue;

            // Precondition[] preconditions = (preconditionAttribute != null
            //     ? preconditionAttribute.PreconditionModules
            //         .ToList().ConvertAll(p => (Precondition)Activator.CreateInstance(p)!).ToArray()
            //     : Array.Empty<Precondition>())!;

            IEnumerable<PreconditionAttribute> preconditions = method.GetCustomAttributes().Select(a => a.GetType())
                .Where(t =>
                    t.IsSubclassOf(typeof(PreconditionAttribute)) && !t.IsAbstract)
                .Select(t => (PreconditionAttribute)Activator.CreateInstance(t)!);
            VoteConfigAttribute voteConfig =
                method.GetCustomAttribute<VoteConfigAttribute>() ?? new VoteConfigAttribute();
            //* -> is command 
            if (!AddCommand(new CommandInfo
                {
                    Name = commandAttribute.Text,
                    Type = CommandType.MessageCommand,
                    Method = method,
                    IsDevOnly = devonly,
                    AutoDeleteOldComponents = oldComponentsAttribute?.AutoDelete ?? false,
                    Preconditions = preconditions.ToArray(),
                    VoteConfig = voteConfig,
                }))
            {
                Console.WriteLine(
                    $"Message Command {commandAttribute.Text} in {moduleType} already exists somewhere else! -> no new command was added");
            }
        }
    }


    /// <summary>
    /// Add a <see cref="InteractionModuleBase"/> and its callback methods
    /// </summary>
    /// <param name="module">The module to add</param>
    private static void AddInteractionModule(InteractionModuleBase module)
    {
        // Console.WriteLine("adding module " + module);
        Type moduleType = module.GetType();
        foreach (MethodInfo method in moduleType.GetMethods())
        {
            LinkComponentInteractionAttribute? linkComponentAttribute =
                method.GetCustomAttribute<LinkComponentInteractionAttribute>();

            //* Only add if the linkComponentAttribute is set
            if (linkComponentAttribute == null) continue;

            //* -> Is component interaction callback
            // Console.WriteLine("  - " + method);

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
    /// Checks if all preconditions of a command are met. Handles unmet responses
    /// </summary>
    /// <param name="commandContext">The command context to check the precondition on</param>
    /// <param name="module">The command contexts module</param>
    /// <returns>True is all preconditions are met, else false</returns>
    private static async Task<bool> CheckPreconditions(ICommandContext commandContext, IModuleBase module)
    {
        CommandInfo commandInfo = commandContext.CommandInfo;
        
        foreach (PreconditionAttribute precondition in commandInfo.Preconditions)
        {
            // Console.WriteLine("Checking precondition " + precondition);
            try
            {
                await precondition.WithContext(commandContext).Satisfy();
            }
            catch (PreconditionNotSatisfiedException e)
            {
                await module.ReplyAsync(e.FormattedMessage);
                return false;
            }
            catch (PreconditionFailedException e)
            {
                if (!e.Responded)
                {
                    // TODO
                }
                return false;
            }
            catch (Exception)
            {
                // Console.WriteLine(e);
                await module.DeleteOriginalResponse();
                return false;
            }
        }

        return true;
    }


    /// <summary>
    /// Execute a given slash command
    /// </summary>
    /// <param name="command">The command</param>
    public static async Task ExecuteSlashCommand(SocketSlashCommand command)
    {
        try
        {
            SlashCommandContext commandContext = new SlashCommandContext(command);
            CommandInfo commandInfo = commandContext.CommandInfo;

            // if (commandContext.CommandInfo is not { OverrideDefer: true })
            // {
            //     commandContext.Command?.DeferAsync(ephemeral: commandInfo is { IsPrivate: true });
            // }

            if (commandInfo is { Method: not null })
            {
                SlashCommandModuleBase module = commandInfo.GetNewModuleInstanceWith(commandContext);
                // module.Context = commandContext;

                Console.WriteLine($"Found /{commandInfo.Name} in {module}");

                object?[] args = commandContext.Arguments;

                await command.DeferAsync(commandContext.IsEphemeral);

                bool preconditionsMet = await CheckPreconditions(commandContext, module);
                if(!preconditionsMet) return;
                
                Console.WriteLine($"Executing with args: {string.Join(", ", args)}");


                // helper.SetContext(commandContext);
                try
                {
                    //* Check if auto delete old components is enabled, and if so, delete
                    if (commandInfo.AutoDeleteOldComponents)
                        _ = commandContext.GuildConfig.DeleteLastInteractionOf(commandInfo); //* Must not be awaited

                    await (Task)commandInfo.Method!.Invoke(module, args)!;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
            else
            {
                await commandContext.Command.ModifyOriginalResponseAsync(properties =>
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
        MessageCommandContext commandContext = new MessageCommandContext(command);
        CommandInfo commandInfo = commandContext.CommandInfo;
        await command.DeferAsync();


        if (commandInfo is { Method: not null })
        {
            MessageCommandModuleBase module = commandInfo.GetNewModuleInstanceWith(commandContext);
            // module.Context = commandContext;
            Console.WriteLine($"Found message command {commandInfo.Name} in {module}");
            object[] args = { commandContext.Message };

            bool preconditionsMet = await CheckPreconditions(commandContext, module);
            
            if(!preconditionsMet) return;
            
            try
            {
                // var module = (ModuleBase) Activator.CreateInstance(commandInfo.Method.Module.GetType());
                // Console.WriteLine(module + " " + commandInfo.NewModule);


                await (Task)commandInfo.Method!.Invoke(module, args)!;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
        else
        {
            await command.ModifyOriginalResponseAsync(properties =>
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
        InteractionContext context = new InteractionContext(component);
        foreach (KeyValuePair<string, List<MethodInfo>> componentCallback in ComponentCallbacks)
        {
            //* Key = the components custom id or * for any id
            //* Value = List of methods to call when the custom id appears

            string key = componentCallback.Key;

            bool startsWith =
                key.Length > 1 &&
                key.Last() == '*'; //* Match bla-id-* to bla-id-123
            bool endsWith =
                key.Length > 1 &&
                key.First() == '*'; // Match *-bla-id to 123-bla-id


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
                    var module = (InteractionModuleBase)Activator.CreateInstance(method.DeclaringType!)!;
                    module.SetContext(context);
                    await (Task)method.Invoke(module, Array.Empty<object>())!;
                }
            }
        }
        // Console.WriteLine(context.DataStore.QueuePage);
        //* Invoke addition component callbacs
        // foreach (var keyValuePair in AdditionalComponentCallbacks.Where(c=>c.Key==component.Data.CustomId))
        // {
        //     keyValuePair.Value.Invoke(context);
        // }
    }

    /// <summary>
    /// Check if the message is a text command and reply with an message to use the application command
    /// </summary>
    /// <param name="socketCommandContext">The command context</param>
    public static void CheckTextCommand(SocketCommandContext socketCommandContext)
    {
        CommandInfo command = Util.GetTextCommandFromMessage(socketCommandContext.Message);

        IDisposable? typing = null;

        if (command.IsDevOnly && socketCommandContext.Guild.Id != Secrets.DevGuildId)
        {
            return;
        }


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

        File.WriteAllText($"{Util.RuntimeDir}/commands.json", JsonSerializer.Serialize(savable));
    }


    /// <summary>
    /// Read local file and try converting the json to valid command infos
    /// </summary>
    /// <returns>The list of commands in the safe-file</returns>
    public static List<CommandInfo> ImportSlashCommands()
    {
        // Console.WriteLine($"Commands: {Util.RuntimeDir}/commands.json");
        List<CommandInfo> commandInfos = new();
        try
        {
            string content = File.ReadAllText($"{Util.RuntimeDir}/commands.json");

            JsonObject? json = JsonSerializer.Deserialize<JsonObject>(content);


            JsonArray? jsonCommands = null;

            if (json != null && json.TryGetPropertyValue("commands", out var jn))
            {
                if (jn != null) jsonCommands = jn.AsArray();
            }

            if (jsonCommands != null)
            {
                foreach (JsonNode? jsonCommand in jsonCommands)
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
        List<CommandInfo> messageCommands = new List<CommandInfo>();

        try
        {
            string content = File.ReadAllText($"{Util.RuntimeDir}/commands.json");

            JsonObject? json = JsonSerializer.Deserialize<JsonObject>(content);

            JsonArray? jsonMessageCommands = null;
            if (json != null && json.TryGetPropertyValue("messageCommands", out var jmcmd))
            {
                jsonMessageCommands = jmcmd?.AsArray();
            }

            if (jsonMessageCommands != null)
            {
                foreach (JsonNode? jsonMsgCommand in jsonMessageCommands)
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
        List<CommandInfo> cmds = ImportSlashCommands();
        cmds.AddRange(ImportMessageCommands());
        return cmds;
    }
}