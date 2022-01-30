﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions;
using GoogleBot.Interactions.Commands;
using GoogleBot.Interactions.Context;
using GoogleBot.Interactions.Modules;

namespace GoogleBot.Services;

/// <summary>
/// Keeps track of a commands preconditions for one guild
/// </summary>
public class PreconditionWatcher
{
    /// <summary>
    /// The command the execute when the vote passes
    /// </summary>
    public CommandInfo CommandInfo { get; }

    private bool running;

    private int requiredVotes = 0;

    private readonly List<ulong> votedUsers = new();

    private readonly GuildConfig guildConfig;

    private object[] commandsArgs = Array.Empty<object>();

    private ICommandContext Context { get; set; }

    private CommandModuleBase Module { get; set; }

    public string Id { get; private set; } = string.Empty;

    /// <summary>
    /// Number of votes needed to pass the vote
    /// </summary>
    public int RemainingVotes => requiredVotes - votedUsers.Count;

    private object[] UsedArgs
    {
        get
        {
            try
            {
                //* Get the used args (remove default values)
                List<object> usedArgs = new();
                // int i = 0;
                for (int i = 0; i < commandsArgs.Length; i++)
                {
                    // Console.WriteLine(args[i] + " != " + CommandInfo.Method!.GetParameters()[i].DefaultValue + " : " +
                    //                   (args[i] != CommandInfo.Method!.GetParameters()[i].DefaultValue));
                    if (!CommandInfo.Method!.GetParameters()[i].HasDefaultValue || commandsArgs[i].ToString() !=
                        CommandInfo.Method!.GetParameters()[i].DefaultValue!.ToString())
                    {
                        usedArgs.Add(commandsArgs[i]);
                    }
                }

                return usedArgs.ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return Array.Empty<object>();
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="PreconditionWatcher"/> for the specified <see cref="Interactions.Commands.CommandInfo"/> and <see cref="GoogleBot.GuildConfig"/>
    /// </summary>
    /// <param name="commandInfo"></param>
    /// <param name="guildConfig"></param>
    public PreconditionWatcher(CommandInfo commandInfo, GuildConfig guildConfig)
    {
        CommandInfo = commandInfo;
        this.guildConfig = guildConfig;
    }

    /// <summary>
    /// Modifies old message and resets Context, Votes, args....
    /// </summary>
    private async Task Reset()
    {
        //* Only delete the message when the voted wasn't completed
        if (running)
        {
            try
            {
                // await Context.Command.ModifyOriginalResponseAsync(properties =>
                // {
                //     properties.Embed = new EmbedBuilder().AddField("Cancelled", "The vote was cancelled").Build();
                //     properties.Components = Optional<MessageComponent>.Unspecified;
                // });
                await (await Context.Respondable.GetOriginalResponseAsync()).DeleteAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        running = false;
        votedUsers.Clear();
        requiredVotes = 0;
        Id = string.Empty;
        Context = null;
        Module = null;
        commandsArgs = Array.Empty<object>();
    }

    /// <summary>
    /// Checks if all preconditions have been met and reply / start votes if needed
    /// </summary>
    /// <param name="context">The commands context</param>
    /// <param name="module">The commands module</param>
    /// <param name="args">The commands args</param>
    /// <returns>True if all conditions have been met</returns>
    public async Task<bool> CheckPreconditions(ICommandContext context, CommandModuleBase module, object[] args)
    {
        // Console.WriteLine(requiresVc + " " + requiresMajority);

        if (CommandInfo.Preconditions.RequiresBotConnected)
        {
            //* Check if user is connected to a VC
            if (context.VoiceChannel == null)
            {
                await ReplyAsync(context, Responses.NoVoiceChannel(), CommandInfo.OverrideDefer);
                return false;
            }

            //* Check if user is connected to the same VC as the bot
            if (guildConfig.BotConnectedToVC && !context.VoiceChannel.Equals(guildConfig.BotsVoiceChannel))
            {
                await ReplyAsync(context, Responses.WrongVoiceChannel(guildConfig.BotsVoiceChannel.Mention));
                return false;
            }
        }

        //* If the commands does not require the majority, return
        if (!CommandInfo.Preconditions.RequiresMajority) return true;

        // await context.Command!.DeferAsync();

        await Reset();

        Context = context;
        Module = module;
        commandsArgs = args;

        var users = await context.VoiceChannel!.GetUsersAsync().ToListAsync().AsTask();
        int userCount = users.First()?.ToList().FindAll(u => !u.IsBot).Count ?? 0;


        requiredVotes = (int)MathF.Ceiling((float)userCount / 2);
        // requiredVotes = 2;
        if (requiredVotes <= 1)
        {
            return true;
        }

        if (context.IsEphemeral)
        {
            await Context.Respondable.ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = Responses.CommandRequiresMajorityEphemeralHint(CommandInfo).BuiltEmbed;
            });
            return false;
        }

        votedUsers.Add(context.User.Id);
        Id =
            $"majority-vote-{CommandInfo.Name}-{guildConfig.Id}-{DateTime.Now.TimeOfDay.TotalMilliseconds}-{Util.RandomString()}";
        running = true;


        await Context.Respondable.ModifyOriginalResponseAsync(properties =>
        {
            properties.Embed = Responses.VoteRequired(Context.User,
                    $"/{CommandInfo.Name}{(UsedArgs.Length <= 0 ? "" : $" {string.Join(" ", UsedArgs)}")}",
                    RemainingVotes)
                .BuiltEmbed;
            properties.Components = new ComponentBuilder()
                .WithButton(CommandInfo.Preconditions.MajorityVoteButtonText, Id, ButtonStyle.Success).Build();
        })!;
        // Console.WriteLine("Vote created");
        return false;
    }


    /// <summary>
    /// if the user didn't vote before, count their vote and check if remaining votes reached or edit message
    /// </summary>
    /// <param name="component">The messages component</param>
    public async Task TryVote(SocketMessageComponent component)
    {
        // Console.WriteLine("TRYING TO VOTE");


        IGuildUser user = component.User as IGuildUser;
        IVoiceChannel vc = guildConfig.BotsVoiceChannel ?? user!.VoiceChannel;

        //* If the id doesnt match or the users isn't connected to the vc, ignore (return)
        var usersInVc = (await vc.GetUsersAsync().ToListAsync().AsTask()).First();

        // Console.WriteLine(component.Data.CustomId + " " + Id);
        // Console.WriteLine(string.Join(", ", usersInVc.ToList().ConvertAll(u=>u.Id)) + " \n me: " + component.User.Id);

        if (component.Data.CustomId != Id || !usersInVc.ToList().ConvertAll(u => u.Id).Contains(component.User.Id))
            return;
        // Console.WriteLine("VOTING");
        if (!votedUsers.Contains(component.User.Id))
            votedUsers.Add(component.User.Id);
        else
            await component.FollowupAsync("`You already voted!`", ephemeral: true);

        await component.Message.ModifyAsync(properties =>
        {
            properties.Embed = Responses.VoteRequired(component.User,
                $"/{CommandInfo.Name}{(UsedArgs.Length <= 0 ? "" : $" {string.Join(" ", UsedArgs)}")}",
                RemainingVotes).BuiltEmbed;
        });

        if (RemainingVotes <= 0)
        {
            running = false;

            //* Remove the button
            await component.Message.ModifyAsync(properties =>
            {
                properties.Components = null;
                properties.Content = "`Executing...`";
            });

            await Invoke(); //* Execute the command
            await Reset(); //* Reset the watcher
        }
    }

    /// <summary>
    /// Execute the command (when all remaining votes == 0)
    /// </summary>
    private async Task Invoke()
    {
        try
        {
            await ((Task)Context.CommandInfo?.Method?.Invoke(Module, commandsArgs ?? Array.Empty<object>())!)!;
        }
        catch (Exception e)
        {
            Console.WriteLine("An error occured executing the command " + Context.CommandInfo);
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }

    /// <summary>
    /// Reply to the command in the given context
    /// </summary>
    /// <param name="context">The commands <see cref="SlashCommandContext"/></param>
    /// <param name="message">The <see cref="FormattedMessage"/> to send</param>
    /// <param name="ephemeralIfPossible">If not deferred yet, do so with ephemeral (or not)</param>
    private async Task ReplyAsync(ICommandContext context, FormattedMessage message, bool ephemeralIfPossible = false)
    {
        try
        {
            await context.Respondable.DeferAsync(ephemeralIfPossible);
        }
        catch (Exception)
        {
            //Ignored}
        }


        await context.Respondable.ModifyOriginalResponseAsync(properties =>
        {
            properties.Embed = message.BuiltEmbed;
            properties.Components = message.BuiltComponents;
            properties.Content = message.Message;
        });
    }
}