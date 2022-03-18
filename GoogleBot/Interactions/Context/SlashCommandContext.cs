using System;
using System.Linq;
using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions.Commands;
using GoogleBot.Services;

namespace GoogleBot.Interactions.Context;

/// <summary>
/// A context for modules using slash command
/// </summary>
public class SlashCommandContext : ICommandContext
{
    public SocketUser User { get; }
    public ISocketMessageChannel TextChannel { get; }
    public IVoiceChannel? VoiceChannel { get; }
    public GuildConfig GuildConfig { get; }
    public SocketGuild Guild { get; }
    public CommandInfo CommandInfo { get; }
    public IDiscordInteraction Respondable => Command;
    public bool IsEphemeral { get; } = false;

    /// <summary>
    /// The arguments for the executed command (including default values for optional args)
    /// </summary>
    public object[] Arguments { get; }

    /// <summary>
    /// The original command 
    /// </summary>
    public SocketSlashCommand Command { get; }
    
    


    /// <summary>
    /// Creates a new <see cref="SlashCommandContext"/> from a <see cref="SocketSlashCommand"/>
    /// </summary>
    /// <param name="command"></param>
    /// <exception cref="ArgumentException"></exception>
    public SlashCommandContext(SocketSlashCommand command)
    {
        IGuildUser guildUser = (command.User as IGuildUser)!;

        TextChannel = command.Channel;

        CommandInfo = InteractionMaster.GetCommandFromName(command.CommandName)!;
        Command = command;
        Guild = (SocketGuild?)guildUser.Guild!;
        User = command.User;
        GuildConfig = GuildConfig.Get(guildUser.GuildId);
        VoiceChannel = guildUser.VoiceChannel;

        object[] args = new object[CommandInfo.Method!.GetParameters().Length];

        object[] options = command.Data.Options.ToList().ConvertAll(option => option.Value).ToArray();


        int i;
        //* Fill the args with the provided option values
        for (i = 0; i < Math.Min(options.Length, args.Length); i++)
        {
            args[i] = options[i];
        }

        //* Fill remaining args with their default values
        for (; i < args.Length; i++)
        {
            if (!CommandInfo.Method.GetParameters()[i].HasDefaultValue)
            {
                throw new ArgumentException("Missing options.");
            }

            args[i] = CommandInfo.Method!.GetParameters()[i].DefaultValue!;
        }

        Arguments = args;

        if (CommandInfo.IsOptionalEphemeral)
        {
            int optionsHidden = Command.Data.Options.ToList()
                .FindAll(o => o.Name.ToLower() == "hidden" && (bool)o.Value).Count;
            if (optionsHidden > 1)
                throw new ArgumentException("Too many options for \"hidden\"");
            IsEphemeral = optionsHidden > 0;
        }
    }
}