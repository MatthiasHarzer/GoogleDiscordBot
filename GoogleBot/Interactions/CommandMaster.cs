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


/// <summary>
/// Keeps track of commands and executes them
/// </summary>
public static class CommandMaster
{
    public static readonly List<CommandInfo> CommandList = new();

    public static readonly List<ApplicationModuleHelper> Helpers = new();


    
    /// <summary>
    /// Gets the application command with the given name
    /// </summary>
    /// <param name="commandName"></param>
    /// <returns>The command or null if not found</returns>
    public static CommandInfo GetCommandFromName(string commandName)
    {
        var res = CommandList.FindAll(c =>c.Name == commandName);
        if (res.Count != 0)
        {
            return res.First();
        }
        
        return null;
    }

    /// <summary>
    /// Get all modules with base class <see cref="ApplicationModuleBase"/> an create their <see cref="ApplicationModuleHelper"/> 
    /// </summary>
    public static void MountModules()
    {
        IEnumerable<ApplicationModuleBase> commandModules = typeof(ApplicationModuleBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ApplicationModuleBase)) && !t.IsAbstract)
            .Select(t => (ApplicationModuleBase)Activator.CreateInstance(t));

        foreach (ApplicationModuleBase module in commandModules)
        {
            Helpers.Add(new ApplicationModuleHelper(module));
        }
    }

    /// <summary>
    /// Execute a given slash command
    /// </summary>
    /// <param name="command">The command</param>
    public static async Task Execute(SocketSlashCommand command)
    {
        object[] args = command.Data.Options.ToList().ConvertAll(option => option.Value).ToArray();

        ApplicationModuleHelper helper = Helpers.Find(helper =>helper.GetCommandsAsText().Contains(command.CommandName));
        Context commandContext = new Context(command);
        CommandInfo commandInfo = commandContext.CommandInfo;
        
        Console.WriteLine($"Found {commandInfo?.Name} in {helper?.Module}");
        
        commandContext.Command?.DeferAsync(ephemeral: commandInfo is {IsPrivate: true});
        
        if (helper != null && commandInfo is {Method: not null})
        {
            Console.WriteLine($"Executing with args: {string.Join(", ", args)}");
            
            helper.SetContext(commandContext);
            try
            {

                await ((Task)commandInfo.Method.Invoke(helper.Module, args))!;
            }
            catch(Exception e)
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
    
    
    

    /// <summary>
    /// Check if the message is a text command and reply with an message to use the application command
    /// </summary>
    /// <param name="socketCommandContext">The command context</param>
    public static void CheckTextCommand(SocketCommandContext socketCommandContext)
    {
        
        CommandConversionInfo commandContext = GetTextCommandInfoFromMessage(socketCommandContext.Message);
        IDisposable typing = null;
        
        ApplicationModuleHelper helper = Helpers.Find(helper => helper.GetCommandsAsText().Contains(commandContext.Command.Name));

        if (helper != null)
        {
            typing = socketCommandContext.Channel.EnterTypingState();
            EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
            embed.AddField("Text commands are deprecated! Please use the application command.",
                $"Consider using `/{commandContext.Command.Name} {string.Join(" ", commandContext.Command.Parameters.ToList().ConvertAll(p => p.IsOptional ? $"[<{p.Name}>]" : $"<{p.Name}>"))}` instead.");
            FormattedMessage message = new FormattedMessage(embed);
            socketCommandContext.Message?.ReplyAsync(message.Message, embed: message.Embed?.Build());
        }
        
        typing?.Dispose();
        
    }
    
}