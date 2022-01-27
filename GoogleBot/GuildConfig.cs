using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions;
using GoogleBot.Interactions.Modules;


namespace GoogleBot;

/// <summary>
/// Keeps track of a commands majority votes in a guild
/// </summary>
public class MajorityWatcher
{
    /// <summary>
    /// The command the execute when the vote passes
    /// </summary>
    public CommandInfo Command { get; }

    private bool running;

    private int requiredVotes = 0;

    private readonly List<ulong> votedUsers = new();

    private string id = string.Empty;

    private readonly GuildConfig guildConfig;

    private object[] commandsArgs = Array.Empty<object>();

    private Context Context { get; set; }
    
    private ApplicationModuleBase Module { get; set; }

    public string Id => id;

    /// <summary>
    /// Number of votes needed to pass the vote
    /// </summary>
    public int RemainingVotes => requiredVotes - votedUsers.Count;

    /// <summary>
    /// Creates a new MajorityWatcher for the specified <see cref="CommandInfo"/> and <see cref="GoogleBot.GuildConfig"/>
    /// </summary>
    /// <param name="command"></param>
    /// <param name="guildConfig"></param>
    public MajorityWatcher(CommandInfo command, GuildConfig guildConfig)
    {
        Command = command;
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
                await Context.Command.ModifyOriginalResponseAsync(properties =>
                {
                    properties.Embed = new EmbedBuilder().AddField("Cancelled", "The vote was cancelled").Build();
                    properties.Components = Optional<MessageComponent>.Unspecified;
                });
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
        id = string.Empty;
        Context = null;
        Module = null;
        commandsArgs = Array.Empty<object>();
    }

    /// <summary>
    /// Checks if a vote is needed for the current command (more than 2 people, in VC etc)
    /// </summary>
    /// <param name="context">The commands context</param>
    /// <param name="module">The commands module</param>
    /// <param name="args">The commands args</param>
    /// <returns>True if a vote started, else False</returns>
    public async Task<bool> CreateVoteIfNeeded(Context context, ApplicationModuleBase module, object[] args)
    {
        // Console.WriteLine(context);
        if (context.VoiceChannel == null)
        {
            // Console.WriteLine("VC NULL");
            return false;
        }

        await Reset();

        Context = context;
        Module = module;
        commandsArgs = args;

        var users = await context.VoiceChannel.GetUsersAsync().ToListAsync().AsTask();
        int userCount = (users.First()?.Count - 1) ?? 0; //* -1 for the bot

        // Console.WriteLine(userCount);
        if (guildConfig.AudioPlayer.AudioClient?.ConnectionState == ConnectionState.Disconnected)
        {
            // Console.WriteLine("Not connected");   
            return false;
        }

        requiredVotes = (int)MathF.Ceiling((float)userCount / 2);
        requiredVotes = 2;
        if (requiredVotes <= 1)
        {
            return false;
        }

        votedUsers.Add(context.User.Id);
        id = $"mv-{Command.Name}-{guildConfig.Id}-{DateTime.Now.TimeOfDay.TotalMilliseconds}-{Util.RandomString()}";
        running = true;

        // Console.WriteLine(id);
        //
        // Console.WriteLine(context.Command);

        // if (Context.Command?.HasResponded == false)
        // {
        //     try
        //     {
        //         await Context.Command.DeferAsync();
        //     }
        //     catch (Exception)
        //     {
        //         //Ignored}
        //     }
        // }

        await Context.Command?.ModifyOriginalResponseAsync(properties =>
        {
            
            properties.Embed = Responses.VoteRequired(Context.User, $"/{Command.Name}{(args.Length <= 0 ? "" : string.Join(" ", args))}", RemainingVotes).BuiltEmbed;
            properties.Components = new ComponentBuilder()
                .WithButton(Command.MajorityVoteButtonText, id, ButtonStyle.Success).Build();
        })!;
        Console.WriteLine("Created");
        return true;
    }

    /// <summary>
    /// if the user didn't vote before, count their vote and check if remaining votes reached or edit message
    /// </summary>
    /// <param name="uid">The users id</param>
    /// <param name="component">The messages component</param>
    public async Task TryVote(ulong uid, SocketMessageComponent component)
    {
        if (component.Data.CustomId != Id)
            return;

        if (!votedUsers.Contains(uid))
            votedUsers.Add(uid);

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
        else
        {
            await component.Message.ModifyAsync(properties =>
            {
                properties.Embed = Responses.SkipVote(RemainingVotes).BuiltEmbed;
            });
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
}

/// <summary>
/// Additional params for a guild, like an AudioPlayer for playing sound
/// </summary>
public class GuildConfig
{
    private static readonly List<GuildConfig> GuildMaster = new();
    public AudioPlayer AudioPlayer { get; }
    public ulong Id { get; }

    private readonly List<MajorityWatcher> watchers = new();

    public MajorityWatcher GetWatcher(CommandInfo command)
    {
        MajorityWatcher w = watchers.Find(w => w.Command.Name == command.Name);
        if (w != null) return w;
        w = new MajorityWatcher(command, this);
        watchers.Add(w);
        return w;
    }

    public MajorityWatcher GetWatcher(string id)
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