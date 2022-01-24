using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;
using Newtonsoft.Json;
using static GoogleBot.Util;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

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
        
        ExportCommands();
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
        
        CommandInfo command = GetTextCommandFromMessage(socketCommandContext.Message);
  
        IDisposable typing = null;
        
        ApplicationModuleHelper helper = Helpers.Find(helper => helper.GetCommandsAsText().Contains(command.Name));

        if (helper != null)
        {
            typing = socketCommandContext.Channel.EnterTypingState();
            EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
            embed.AddField("Text commands are deprecated! Please use the application command.",
                $"Consider using `/{command.Name} {string.Join(" ", command.Parameters.ToList().ConvertAll(p => p.IsOptional ? $"[<{p.Name}>]" : $"<{p.Name}>"))}` instead.");
            FormattedMessage message = new FormattedMessage(embed);
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
        foreach (CommandInfo cmd in CommandList)
        {
            commands.Add(cmd.ToJson());
        }
        savable.Add("commands", commands);
        // Console.WriteLine(JsonSerializer.Serialize(savable));
        
        File.WriteAllText("./commands.json", JsonSerializer.Serialize(savable));
    }

    
    /// <summary>
    /// Read local file and try converting the json to valid command infos
    /// </summary>
    /// <returns></returns>
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
                    if (jsonCommand != null)
                    {
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

        }

        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            // -> something's fishy with the file or json
            
        }
        return commandInfos;
        
    }
}