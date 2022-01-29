﻿using System.Linq;
using Discord;
using Discord.WebSocket;


namespace GoogleBot.Interactions.Modules;

public class Responses
{
    /// <summary>
    /// For a skip that needs the majority of users in the VC to agree 
    /// </summary>
    /// <param name="numberOfVotesRequired">Number of required votes</param>
    /// <returns>A FormattedMessage</returns>
    public static FormattedMessage SkipVote(int numberOfVotesRequired)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Skip vote started",
            $"`{numberOfVotesRequired}` more {(numberOfVotesRequired == 1 ? "vote" : "votes")} required to skip"));
    }

    /// <summary>
    /// For when a song was skipped
    /// </summary>
    /// <param name="skippedSong">The skipped song</param>
    /// <returns>A FormattedMessage</returns>
    public static FormattedMessage Skipped(string skippedSong)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Skipping", $"Song {skippedSong} skipped"));
    }

    public static FormattedMessage NothingToSkip()
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Nothing to skip", "The queue is empty."));
    }

    public static FormattedMessage VoteRequired(SocketUser author, string command, int numberOfVotesRequired)
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
            $"`{numberOfVotesRequired}` more {(numberOfVotesRequired == 1 ? "vote" : "votes")} required",
            $"<@{author.Id}> wants to execute `{command}`"));
    }

    /// <summary>
    /// A hint when using text-based commands, to use application command instead
    /// </summary>
    /// <param name="command">The command the user wanted to execute</param>
    /// <returns>A FormattedMessage</returns>
    public static FormattedMessage DeprecationHint(CommandInfo command)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Text-based commands are deprecated!",
            $"Please use the application command `{Util.FormattedCommand(command)}` instead.")
        );
    }
    
    public static FormattedMessage NoVoiceChannel()
    {
        return new FormattedMessage(new EmbedBuilder().AddField("No voice channel",
            "`Please connect to a voice channel first!`"));
    }

    public static FormattedMessage WrongVoiceChannel(string targetVc)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Invalid voice channel",
            $"You have to be connect to the same voice channel {targetVc} as the bot."));
    }

    public static FormattedMessage CommandRequiresMajorityEphemeralHint(CommandInfo commandInfo)
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
            $"The command `/{commandInfo.Name}` requires the consent of the majority of members in your voice channel to execute",
            "Consider removing the `hidden` option from the command to trigger a vote"));
    }
}