﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;
using static GoogleBot.Util;

namespace GoogleBot.Interactions;

public class CommandModuleHelper
{
    
    
    public CommandModuleBase Module { get;  }

    public List<CommandInfo> Commands { get; } = new();

    public Dictionary<string, List<MethodInfo>> ComponentCallbacks { get; } = new();


    public List<string> GetCommandsAsText()
    {
        return Commands.ConvertAll(c => c.Name);
    }

    public CommandModuleHelper(CommandModuleBase module)
    {
        CommandMaster.Helpers.Add(this);
        Console.WriteLine("new CommandModuleHelper for " + module.ToString());
        Module = module;



        foreach (MethodInfo method in Module.GetType().GetMethods())
        {
            CommandAttribute commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            SummaryAttribute summaryAttribute = method.GetCustomAttribute<SummaryAttribute>();
            // AliasAttribute aliasAttribute = method.GetCustomAttribute<AliasAttribute>();
            PrivateAttribute privateAttribute = method.GetCustomAttribute<PrivateAttribute>();
            LinkComponentInteractionAttribute linkComponentAttribute = method.GetCustomAttribute<LinkComponentInteractionAttribute>();
            ParameterInfo[] parameterInfo = method.GetParameters().ToList().ConvertAll(p => new ParameterInfo
            {
                Summary = (p.GetCustomAttribute<SummaryAttribute>()?.Text ?? p.Name) ?? string.Empty,
                Type = p.ParameterType,
                Name = p.Name ?? string.Empty,
                IsMultiple = p.GetCustomAttribute<MultipleAttribute>()?.IsMultiple ?? false,
                IsOptional = p.HasDefaultValue,
            }).ToArray();

            // Console.WriteLine(method.Name + "\n Method -> " + method.ReturnType + "\n W´Type ->" + typeof(Task<CommandReturnValue>) +"\n = " +
            //                   (method.ReturnType == typeof(Task<CommandReturnValue>)) + "\n-----");
            if (commandAttribute != null)
            {
                //* -> is command trigger method
                bool isEphemeral = privateAttribute?.IsEphemeral != null && privateAttribute.IsEphemeral;

                if (!AddCommand(new CommandInfo
                    {
                        Name = commandAttribute.Text,
                        Summary = summaryAttribute?.Text,
                        Parameters = parameterInfo,
                        Method = method,
                        IsPrivate = isEphemeral,
                    }))
                {
                    Console.WriteLine($"Command {commandAttribute.Text} in {Module} already exists somewhere else! -> no new command was added");
                } 

            }else if (linkComponentAttribute != null)
            {
                //* -> Is component interaction callback
                string customId = linkComponentAttribute.CustomId;

                if (ComponentCallbacks.Keys.Contains(customId))
                {
                    ComponentCallbacks[customId].Add(method);
                }
                else
                {
                    ComponentCallbacks.Add(customId, new List<MethodInfo> {method});
                }
            }
        }
    }

    public void SetContext(Context context)
    {
        Module.SetContext(context);
    }
    
    private bool AddCommand(CommandInfo commandInfo)
    {
        if (CommandMaster.CommandsList.FindAll(com => com.Name == commandInfo.Name).Count != 0) return false;
        //* The command does not exist yet -> add
        CommandMaster.CommandsList.Add(commandInfo);
        Commands.Add(commandInfo);
        return true;

    }

    public async Task CallLinkedInteractions(SocketMessageComponent component)
    {
        foreach (KeyValuePair<string, List<MethodInfo>> componentCallback in ComponentCallbacks)
        {
            //* Key = the components custom id or * for any id
            //* Value = List of methods to call when the custom id appears

            if (componentCallback.Key == component.Data.CustomId || componentCallback.Key == "*")
            {
                foreach(MethodInfo method in componentCallback.Value)
                {
                    method.Invoke(Module, new object[] { });
                }
            }
            
        }
    }

    public static async Task InteractionHandler(SocketMessageComponent component)
    {
        foreach (CommandModuleHelper helper in CommandMaster.Helpers)
        {
            await helper.CallLinkedInteractions(component);
        }
    }
}


/// <summary>
/// Keeps track of commands and executes them
/// </summary>
public static class CommandMaster
{
    /// <summary>
    /// Stores all commands with name, aliases, summary and execute methode
    /// </summary>
    public static readonly List<CommandInfo> LegacyCommandsList = new();

    public static readonly List<CommandInfo> CommandsList = new();
    
    public static readonly List<CommandModuleHelper> Helpers = new();

    /// <summary>
    /// Gets the command with the name or null
    /// </summary>
    /// <param name="commandName">Command name (primary)</param>
    /// <returns>The CommandInfo object or null if not found</returns>
    public static CommandInfo GetLegacyCommandFromName(string commandName)
    {
        var res = LegacyCommandsList.FindAll(c => c.Name == commandName);
        if (res.Count != 0)
        {
            return res.First();
        }
        
        return null;
    }
    
    public static CommandInfo GetCommandFromName(string commandName)
    {
        var res = CommandsList.FindAll(c => c.Name == commandName);
        if (res.Count != 0)
        {
            return res.First();
        }
        
        return null;
    }

    /// <summary>
    /// Add a command to the commands list if it does not exist yet 
    /// </summary>
    /// <param name="commandInfo">The command to add</param>
    /// <returns>True if the command where added</returns>
    private static bool AddCommand(CommandInfo commandInfo)
    {
        if (LegacyCommandsList.FindAll(com => com.Name == commandInfo.Name).Count != 0) return false;
        //* The command does not exist yet -> add
        LegacyCommandsList.Add(commandInfo);
        return true;

    }

    /// <summary>
    /// Get all commands and add there info (name, aliases, summery, params)
    /// </summary>
    public static void InstantiateCommands()
    {
        AddApplicationCommands();
        // Console.WriteLine("Instantiating");
        // commands = new Commands();
        foreach (MethodInfo method in typeof(LegacyCommands).GetMethods())
        {
            CommandAttribute commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            SummaryAttribute summaryAttribute = method.GetCustomAttribute<SummaryAttribute>();
            AliasAttribute aliasAttribute = method.GetCustomAttribute<AliasAttribute>();
            PrivateAttribute privateAttribute = method.GetCustomAttribute<PrivateAttribute>();
            SlashOnlyCommandAttribute slashOnlyCommandAttribute =
                method.GetCustomAttribute<SlashOnlyCommandAttribute>();
            ParameterInfo[] parameterInfo = method.GetParameters().ToList().ConvertAll(p => new ParameterInfo
            {
                Summary = p.GetCustomAttribute<SummaryAttribute>()?.Text,
                Type = p.ParameterType.IsArray ? p.ParameterType.GetElementType() : p.ParameterType,
                Name = p.Name,
                IsMultiple = p.IsDefined(typeof(ParamArrayAttribute), false),
                IsOptional = p.HasDefaultValue,
            }).ToArray();

            // Console.WriteLine(method.Name + "\n Method -> " + method.ReturnType + "\n W´Type ->" + typeof(Task<CommandReturnValue>) +"\n = " +
            //                   (method.ReturnType == typeof(Task<CommandReturnValue>)) + "\n-----");
            if (commandAttribute != null && method.ReturnType == typeof(Task<CommandReturnValue>))
            {
                bool isEphemeral = privateAttribute?.IsEphemeral != null && privateAttribute.IsEphemeral;
                bool isSlashOnly = slashOnlyCommandAttribute?.IsSlashOnlyCommand != null &&
                                   slashOnlyCommandAttribute.IsSlashOnlyCommand;
                List<string> aliases = aliasAttribute?.Aliases?.ToList() ?? new List<string>();
                aliases.Insert(0, commandAttribute.Text);

                if (!AddCommand(new CommandInfo
                    {
                        Name = commandAttribute.Text,
                        Summary = summaryAttribute?.Text,
                        Aliases = aliases.ToArray(),
                        Parameters = parameterInfo,
                        Method = method,
                        IsPrivate = isEphemeral,
                        IsSlashOnly = isSlashOnly,
                    }))
                {
                    Console.WriteLine($"Command {commandAttribute.Text} already exists! -> no new command was added");
                }
            }

        }
    }

    public static void AddApplicationCommands()
    {
        IEnumerable<CommandModuleBase> commandModules = typeof(CommandModuleBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(CommandModuleBase)) && !t.IsAbstract)
            .Select(t => (CommandModuleBase)Activator.CreateInstance(t));

        foreach (CommandModuleBase module in commandModules)
        {
            Helpers.Add(new CommandModuleHelper(module));
        }
    }

    public static void Execute(SocketSlashCommand command)
    {
        object[] args = command.Data.Options.ToList().ConvertAll(option => option.Value).ToArray();

        CommandModuleHelper helper = Helpers.Find(helper => helper.GetCommandsAsText().Contains(command.CommandName));
        Context commandContext = new Context(command);
        CommandInfo commandInfo = commandContext.CommandInfo;
        
        if (helper != null && commandInfo is {Method: not null})
        {
            commandContext.Command?.DeferAsync(ephemeral: commandInfo is {IsPrivate: true});
            helper.SetContext(commandContext);
            commandInfo.Method.Invoke(helper.Module, args);
        }
    }

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        Execute(command);
    }

    /// <summary>
    /// Executes an command depending on the ExecuteContext
    /// </summary>
    /// <param name="context">The ExecuteContext with User, Guild, Command etc...</param>
    /// <param name="args">The arguments for the command</param>
    /// <returns>A CommandReturnValue with an embed or text message</returns>
    public static async Task<CommandReturnValue> LegacyExecute(ExecuteContext context, params object[] args)
    {
        
        if (context.Command != null)
        {
            // Console.WriteLine(string.Join(", ", args));
            // Console.WriteLine(args.GetType());
            Console.WriteLine($"Executing '{context.Command.Name}' with args <{string.Join(", ", args)}>");
            try
            {
       
                // Console.WriteLine(RandomColor());
                return await ((Task<CommandReturnValue>)context.Command.Method.Invoke(new LegacyCommands(context),
                    args.ToArray()))!;
            }catch (Exception e)
            {
                Console.WriteLine(e.Message + " " + e.StackTrace);
            }
        }
     
        
        return null;
    }
}