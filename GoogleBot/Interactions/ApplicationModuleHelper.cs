#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;
using static GoogleBot.Util;

namespace GoogleBot.Interactions;


/// <summary>
/// Each instance of <see cref="ApplicationModuleHelper"/> refers to one module
/// </summary>
public class ApplicationModuleHelper
{
    /// <summary>
    /// An instance of the derived module-class 
    /// </summary>
    public ApplicationModuleBase Module { get;  }

    /// <summary>
    /// All command in the module, marked with <see cref="CommandAttribute"/>
    /// </summary>
    public List<CommandInfo> Commands { get; } = new();

    /// <summary>
    /// A dictionary, where the Key is a custom id from a component and the Value a list of all methods
    /// to call, when this key appears. Where Key == * -> all ids
    /// </summary>
    public Dictionary<string, List<MethodInfo>> ComponentCallbacks { get; } = new();


    /// <summary>
    /// Converts the list of CommandInfos to a string list
    /// </summary>
    /// <returns>A list of the modules command names</returns>
    public List<string> GetCommandsAsText()
    {
        return Commands.ConvertAll(c => c.Name);
    }

    /// <summary>
    /// Creates a new instance from the module
    /// </summary>
    /// <param name="module">An instance of the derived module-class</param>
    public ApplicationModuleHelper(ApplicationModuleBase module)
    {
        // Console.WriteLine("new CommandModuleHelper for " + module.ToString());
        Module = module;
        

        //* Get all methods in the module
        foreach (MethodInfo method in Module.GetType().GetMethods())
        {
            CommandAttribute? commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            SummaryAttribute? summaryAttribute = method.GetCustomAttribute<SummaryAttribute>();
            // AliasAttribute aliasAttribute = method.GetCustomAttribute<AliasAttribute>();
            PrivateAttribute? privateAttribute = method.GetCustomAttribute<PrivateAttribute>();
            LinkComponentInteractionAttribute? linkComponentAttribute = method.GetCustomAttribute<LinkComponentInteractionAttribute>();
            ParameterInfo[] parameterInfo = method.GetParameters().ToList().ConvertAll(p => new ParameterInfo
            {
                Summary = (p.GetCustomAttribute<SummaryAttribute>()?.Text ?? p.Name) ?? string.Empty,
                Type = p.GetCustomAttribute<OptionTypeAttribute>()?.Type ?? ToOptionType(p.ParameterType),
                Name = p.GetCustomAttribute<NameAttribute>()?.Text ?? p.Name ?? string.Empty,
                IsMultiple = p.GetCustomAttribute<MultipleAttribute>()?.IsMultiple ?? false,
                IsOptional = p.HasDefaultValue,
            }).ToArray();


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
                            Parameters = parameterInfo,
                            Method = method,
                            IsPrivate = isEphemeral,
                            OverrideDefer = overrideDefer,
                        }))
                    {
                        Console.WriteLine(
                            $"Command {commandAttribute.Text} in {Module} already exists somewhere else! -> no new command was added");
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
    /// Sets the context of the module
    /// </summary>
    /// <param name="context">The new context</param>
    public void SetContext(Context context)
    {
        Module.Context = context;
    }
    
    /// <summary>
    /// Add a command to the commands list if it does not exist yet 
    /// </summary>
    /// <param name="commandInfo">The command to add</param>
    /// <returns>True if the command where added</returns>
    private bool AddCommand(CommandInfo commandInfo)
    {
        if (CommandMaster.CommandList.FindAll(com => com.Name == commandInfo.Name).Count != 0) return false;
        //* The command does not exist yet -> add
        CommandMaster.CommandList.Add(commandInfo);
        Commands.Add(commandInfo);
        return true;

    }

    /// <summary>
    /// Calls all linked interaction where the components custom-id matches
    /// </summary>
    /// <param name="component">The component </param>
    private async Task CallLinkedInteractions(SocketMessageComponent component)
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
                    await (Task)method.Invoke(Module, new object?[]{component})!;
                }
            }
            
        }
    }

    /// <summary>
    /// Calls every linked method in all modules where the components custom-id matches
    /// </summary>
    /// <param name="component">The component</param>
    public static async Task InteractionHandler(SocketMessageComponent component)
    {
        foreach (ApplicationModuleHelper helper in CommandMaster.Helpers)
        {
            await helper.CallLinkedInteractions(component);
        }
    }
}

