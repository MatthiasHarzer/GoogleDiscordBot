using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;

namespace GoogleBot.Interactions;


/// <summary>
/// Keeps track of different Modules of the <see cref="GoogleBot"/>
/// </summary>
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
        // Console.WriteLine($"Searching interations with c id {component.Data.CustomId}");
        foreach (KeyValuePair<string, List<MethodInfo>> componentCallback in ComponentCallbacks)
        {
            //* Key = the components custom id or * for any id
            //* Value = List of methods to call when the custom id appears
            
            // Console.WriteLine($"{componentCallback.Key}: {string.Join(", ", componentCallback.Value.ConvertAll(m=>m.Name))}");

            if (componentCallback.Key == component.Data.CustomId || componentCallback.Key == "*")
            {
                // Console.WriteLine($"FOUND {string.Join(", ", componentCallback.Value.ConvertAll(m=>m.Name))}");
                foreach(MethodInfo method in componentCallback.Value)
                {
                    Module.Context.Component = component;
                    method.Invoke(Module, new []{component});
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

