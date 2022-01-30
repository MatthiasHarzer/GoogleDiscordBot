using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions;
using GoogleBot.Interactions.Modules;



namespace GoogleBot;

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

    private Context Context { get; set; }

    private ModuleBase Module { get; set; }

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
    /// Creates a new <see cref="PreconditionWatcher"/> for the specified <see cref="GoogleBot.CommandInfo"/> and <see cref="GoogleBot.GuildConfig"/>
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
        if (Context is { Command: { HasResponded: true } } && running)
        {
            try
            {
                // await Context.Command.ModifyOriginalResponseAsync(properties =>
                // {
                //     properties.Embed = new EmbedBuilder().AddField("Cancelled", "The vote was cancelled").Build();
                //     properties.Components = Optional<MessageComponent>.Unspecified;
                // });
                await (await Context.Command.GetOriginalResponseAsync()).DeleteAsync();
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
    public async Task<bool> CheckPreconditions(Context context, ModuleBase module, object[] args)
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
        requiredVotes = 2;
        if (requiredVotes <= 1)
        {
            return true;
        }

        if (context.IsEphemeral)
        {
            await Context.Command!.ModifyOriginalResponseAsync(properties =>
            {
                properties.Embed = Responses.CommandRequiresMajorityEphemeralHint(CommandInfo).BuiltEmbed;
            });
            return false;
        }

        votedUsers.Add(context.User.Id);
        Id = $"mv-{CommandInfo.Name}-{guildConfig.Id}-{DateTime.Now.TimeOfDay.TotalMilliseconds}-{Util.RandomString()}";
        running = true;


        await Context.Command?.ModifyOriginalResponseAsync(properties =>
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
        //* If the id doesnt match or the users isn't connected to the vc, ignore (return)
        var usersInVc = (await guildConfig.BotsVoiceChannel.GetUsersAsync().ToListAsync().AsTask()).First();
        if (component.Data.CustomId != Id || usersInVc.ToList().ConvertAll(u=>u.Id).Contains(component.Id))
            return;

        if (!votedUsers.Contains(component.User.Id))
            votedUsers.Add(component.User.Id);

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
    /// <param name="context">The commands <see cref="GoogleBot.Context"/></param>
    /// <param name="message">The <see cref="FormattedMessage"/> to send</param>
    /// <param name="ephemeralIfPossible">If not deferred yet, do so with ephemeral (or not)</param>
    private async Task ReplyAsync(Context context, FormattedMessage message, bool ephemeralIfPossible = false)
    {
        if (!context.Command!.HasResponded)
        {
            try
            {
                await context.Command.DeferAsync(ephemeralIfPossible);
            }
            catch (Exception)
            {
                //Ignored}
            }
        }

        await context.Command.ModifyOriginalResponseAsync(properties =>
        {
            properties.Embed = message.BuiltEmbed;
            properties.Components = message.BuiltComponents;
            properties.Content = message.Message;
        });
    }
}

/// <summary>
/// Additional params for a guild, like an AudioPlayer for playing sound
/// </summary>
public class GuildConfig
{
    private static readonly List<GuildConfig> GuildMaster = new();
    public AudioPlayer AudioPlayer { get; }
    public ulong Id { get; }

    private readonly List<PreconditionWatcher> watchers = new();

    public bool BotConnectedToVC => BotsVoiceChannel != null;

    public IVoiceChannel BotsVoiceChannel => AudioPlayer.VoiceChannel;

    public PreconditionWatcher GetWatcher(CommandInfo command)
    {
        PreconditionWatcher w = watchers.Find(w => w.CommandInfo.Id == command.Id);
        if (w != null) return w;
        w = new PreconditionWatcher(command, this);
        watchers.Add(w);
        return w;
    }

    public PreconditionWatcher GetWatcher(string id)
    {
        return watchers.Find(w => w.Id == id);
    }

    public List<ulong> VotedUsers { get; set; } = new();
    public int RequiredVotes { get; set; } = 0;

    public string ValidSkipVoteId { get; set; } = string.Empty;

    public void GenerateSkipId()
    {
        ValidSkipVoteId = $"sv-{Id}-{DateTime.Now.TimeOfDay.TotalMilliseconds}-{Util.RandomString()}";
    }

    public void InvalidateVoteData()
    {
        VotedUsers.Clear();
        RequiredVotes = 0;
        ValidSkipVoteId = null;
    }


    private GuildConfig(ulong id)
    {
        AudioPlayer = new AudioPlayer();
        Id = id;
        GuildMaster.Add(this);
    }


    /// <summary>
    /// Creates or gets existing Guild object with the ID
    /// </summary>
    /// <param name="guildId">The guilds ID</param>
    /// <returns>New or existing guild object</returns>
    public static GuildConfig Get(ulong? guildId)
    {
        if (guildId == null)
            return null;
        return GuildMaster.Find(guild => guild.Id.Equals(guildId)) ?? new GuildConfig((ulong)guildId);
    }

    // public static GuildConfig Get(SocketGuild guild)
    // {
    //     
    //     return GuildMaster.Find(g => g.Id.Equals(guild.Id)) ?? new GuildConfig((ulong)guildId);
    // }
}