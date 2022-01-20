using System;
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


/// <summary>
/// Keeps track of commands and executes them
/// </summary>
public static class CommandMaster
{
    /// <summary>
    /// Stores all commands with name, aliases, summary and execute methode
    /// </summary>
    public static readonly List<CommandInfo> CommandsList = new();

    /// <summary>
    /// Gets the command with the name or null
    /// </summary>
    /// <param name="commandName">Command name (primary)</param>
    /// <returns>The CommandInfo object or null if not found</returns>
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
        if (CommandsList.FindAll(com => com.Name == commandInfo.Name).Count != 0) return false;
        //* The command does not exist yet -> add
        CommandsList.Add(commandInfo);
        return true;

    }

    /// <summary>
    /// Get all commands and add there info (name, aliases, summery, params)
    /// </summary>
    public static void InstantiateCommands()
    {
     
        // Console.WriteLine("Instantiating");
        // commands = new Commands();
        foreach (MethodInfo method in typeof(Commands).GetMethods())
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

    /// <summary>
    /// Executes an command depending on the ExecuteContext
    /// </summary>
    /// <param name="context">The ExecuteContext with User, Guild, Command etc...</param>
    /// <param name="args">The arguments for the command</param>
    /// <returns>A CommandReturnValue with an embed or text message</returns>
    public static async Task<CommandReturnValue> Execute(ExecuteContext context, params object[] args)
    {
        
        if (context.Command != null)
        {
            // Console.WriteLine(string.Join(", ", args));
            // Console.WriteLine(args.GetType());
            Console.WriteLine($"Executing '{context.Command.Name}' with args <{string.Join(", ", args)}>");
            try
            {
       
                // Console.WriteLine(RandomColor());
                return await ((Task<CommandReturnValue>)context.Command.Method.Invoke(new Commands(context),
                    args.ToArray()))!;
            }catch (Exception e)
            {
                Console.WriteLine(e.Message + " " + e.StackTrace);
            }
        }
     
        
        return null;
    }
}