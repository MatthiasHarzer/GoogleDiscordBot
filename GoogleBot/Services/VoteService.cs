
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions;
using GoogleBot.Interactions.Commands;
using GoogleBot.Interactions.Context;
using Timer = System.Timers.Timer;

namespace GoogleBot.Services;


/// <summary>
/// A <see cref="GuildConfig">GuildConfigs</see> vote service, responsible for votes (majority votes)
/// </summary>
public class VoteService
{
    /// <summary>
    /// A vote of a command (one vote per command per guild)
    /// </summary>
    private class Vote
    {
        private static readonly List<Vote> RunningVotes = new List<Vote>();
        public static readonly string VoteId = "MajorityVote";
        
        private CommandInfo CommandInfo { get; }
        private ICommandContext? Context { get; set; }
        private GuildConfig GuildConfig => Context!.GuildConfig;
        
        /// <summary>
        /// Number of votes needed to pass the vote
        /// </summary>
        private int RemainingVotes => requiredVotes - votedUsers.Count;

        private int requiredVotes;
        
        private readonly List<ulong> votedUsers = new List<ulong>();

        private string Id { get; set; } = string.Empty;
        
        private int timeout = 60; //seconds

        private bool running;

        private Vote(CommandInfo commandInfo)
        {
            RunningVotes.Add(this);
            CommandInfo = commandInfo;
        }
        
        /// <summary>
        /// Gets or creates a new <see cref="Vote"/> for a given command
        /// </summary>
        /// <param name="commandInfo">The command to create/get the vote for</param>
        /// <returns>The vote</returns>
        public static async Task<Vote> GetOrCreate(CommandInfo commandInfo)
        {
            foreach (Vote v in RunningVotes.Where(v => v.CommandInfo.Id == commandInfo.Id))
            {
                await v.Reset();
                return v;
            }

            return new Vote(commandInfo);
        }

        /// <summary>
        /// Tries getting the vote given a vote id 
        /// </summary>
        /// <param name="id">The votes id</param>
        /// <returns>The vote or null if not found</returns>
        public static Vote? GetById(string id)
        {
            return RunningVotes.FirstOrDefault(runningVote => runningVote.Id == id);
        }
        
        /// <summary>
        /// Resets this commands vote
        /// </summary>
        private async Task Reset()
        {
            GuildConfig.Timer.Stop(VoteId);
            
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
                    await (await Context?.Respondable.GetOriginalResponseAsync()!).DeleteAsync();
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
            
        }
        
        
        /// <summary>
        /// Creates a new vote for this <see cref="GoogleBot.Interactions.Commands.CommandInfo"/>
        /// </summary>
        /// <param name="context">The commands context</param>
        /// <param name="reqVotes">The number of votes required to pass it</param>
        /// <param name="to">The duration of the, after which it will timeout if not succeeded</param>
        public async Task Create(ICommandContext context, int reqVotes, int to = 60)
        {
            await Reset();
            to = Math.Max(to, 30);
            timeout = to;
            Context = context;
            requiredVotes = reqVotes;
            
            Id =
                $"vote-{CommandInfo.Name}-{GuildConfig.Id}-{DateTime.Now.TimeOfDay.TotalMilliseconds}-{Util.RandomString()}";
            votedUsers.Add(context.User.Id);
            running = true;
            
            await context.Respondable.ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = Responses.VoteRequired(Context!.User,
                    Util.FormattedUsedCommand(Context),
                    RemainingVotes, timeout).BuiltEmbed;
                properties.Components = new ComponentBuilder()
                    .WithButton(CommandInfo.VoteConfig.ButtonText, Id, ButtonStyle.Success).Build();
            })!;
            
            StopVoteIn(timeout * 1000);
        }
        

        /// <summary>
        /// Waits for the vote to complete (success / lose)
        /// </summary>
        public async Task<bool> AwaitResult()
        {
            while (running)
            {
                await Task.Delay(100);
            }

            return RemainingVotes <= 0;
        }
        
        /// <summary>
        /// Complete the vote and delete the vote button
        /// </summary>
        private async Task CompleteVote()
        {
            running = false;
            
            //* Remove the button
            if(Context != null)
                await (await Context.Respondable.GetOriginalResponseAsync()).ModifyAsync(properties =>
                {
                    properties.Components = new ComponentBuilder().Build();
                    // properties.Content = "`Executing...`";
                });
            
            await Reset();
        }
        
        /// <summary>
        /// Sets a timer to stop the vote after a given amount of time
        /// </summary>
        /// <param name="milliseconds">The time to wait until stopping the vote</param>
        private void StopVoteIn(long milliseconds)
        {
            
            GuildConfig.Timer.Run(() =>
            {
                try
                {
                    _ = CancelVote();
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    //ignored
                }
            }, VoteId).In(milliseconds: milliseconds)
                .Start();
        }

        /// <summary>
        /// Cancels the currently running vote and returns a failed message
        /// </summary>
        private async Task CancelVote()
        {
            if(Context == null) return;
            await (await Context.Respondable.GetOriginalResponseAsync()).ModifyAsync(properties =>
            {
                properties.Embed = Responses.VoteTimedOut(Context).BuiltEmbed;
                properties.Components = new ComponentBuilder().Build();
            });
            await CompleteVote();

        }

        
        
        /// <summary>
        /// if the user didn't vote before, count their vote and check if remaining votes reached or edit message
        /// </summary>
        /// <param name="component">The messages component</param>
        public async Task TryVote(SocketMessageComponent component)
        {
            // Console.WriteLine("TRYING TO VOTE");
            if(!running || Context == null) return;

            IGuildUser? user = component.User as IGuildUser;
            IVoiceChannel vc = Context.GuildConfig.BotsVoiceChannel ?? Context.User.VoiceChannel;

            //* If the id doesnt match or the users isn't connected to the vc, ignore (return)
            var usersInVc = (await vc.GetUsersAsync().ToListAsync().AsTask()).First();
        
            if (component.Data.CustomId != Id)
                return;
       
            if (!usersInVc.ToList().ConvertAll(u => u.Id).Contains(user!.Id))
            {
                
                await component.FollowupAsync(embed: Responses.WrongVoiceChannel(vc).BuiltEmbed, ephemeral: true);
                return;
            }
            // Console.WriteLine("VOTING");
            if (!votedUsers.Contains(component.User.Id))
                votedUsers.Add(component.User.Id);
            else
                await component.FollowupAsync(embed:Responses.AlreadyVoted().BuiltEmbed, ephemeral: true);

            await component.Message.ModifyAsync(properties =>
            {
                properties.Embed = Responses.VoteRequired(Context!.User,
                    Util.FormattedUsedCommand(Context),
                    RemainingVotes, timeout).BuiltEmbed;
            });

            if (RemainingVotes <= 0)
            {
                await CompleteVote();
            }
        }
    }

    private GuildConfig Guild { get; }

    public VoteService(GuildConfig guild)
    {
        Guild = guild;
    }

    
    /// <summary>
    /// Checks if a majority vote is needed and possible and starts one if so
    /// </summary>
    /// <param name="context">The commands context to check/set the majority</param>
    /// <param name="timeout">The timeout, when the vote will fail</param>
    /// <returns>True if the vote passed, else false</returns>
    public async Task<bool> AwaitMajorityCommandVote(ICommandContext context, int timeout = 60)
    {
        var users = await context.VoiceChannel!.GetUsersAsync().ToListAsync().AsTask();
        int userCount = users.First()?.ToList().FindAll(u => !u.IsBot).Count ?? 0;
        
        int requiredVotes = (int)MathF.Ceiling((float)userCount / 2);
        // requiredVotes = 2;
        if (requiredVotes <= 1)
        {
            return true;
        }

        if (!context.CommandInfo.VoteConfig.Enabled)
        {
            await context.Respondable.ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = Responses.VoteDisabled(context).BuiltEmbed;
            });
            return false;
        }

        Vote vote = await Vote.GetOrCreate(context.CommandInfo);
        await vote.Create(context, requiredVotes, timeout);
        return await vote.AwaitResult();
    }

    /// <summary>
    /// Tries to vote given a message component
    /// </summary>
    /// <param name="component">The message component</param>
    public Task TryVote(SocketMessageComponent component)
    {
        string id = component.Data.CustomId;
        Vote? vote = Vote.GetById(id);
        vote?.TryVote(component);
        return Task.CompletedTask;
    }
}