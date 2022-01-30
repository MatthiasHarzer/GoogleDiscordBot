using System;
using System.Linq;
using Discord;
using Discord.WebSocket;
using GoogleBot.Interactions.Commands;
using YoutubeExplode.Videos;
using GoogleBot.Interactions.Commands;
using GoogleBot.Services;


namespace GoogleBot.Interactions;

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
    /// <param name="nextSong">The skipped song</param>
    /// <returns>A FormattedMessage</returns>
    public static FormattedMessage Skipped(Video nextSong)
    {
        return nextSong == null
            ? new FormattedMessage(new EmbedBuilder().AddField("Skipping", $"The queue is empty -> disconnecting"))
            : new FormattedMessage(new EmbedBuilder().AddField("Skipping",
                $"Now playing {Util.FormattedLinkedVideo(nextSong)}"));
    }

    public static FormattedMessage FromPlayReturnValue(PlayReturnValue playReturnValue)
    {
        EmbedBuilder embed = new();
        switch (playReturnValue.AudioPlayState)
        {
            case AudioPlayState.Success:
                embed.AddField("Now playing", Util.FormattedLinkedVideo(playReturnValue.Video));
                break;
            case AudioPlayState.PlayingAsPlaylist:
                embed.WithTitle($"Added {playReturnValue.Videos?.Length} songs to queue");
                embed.AddField("Now playing", Util.FormattedLinkedVideo(playReturnValue.Video));
                break;
            case AudioPlayState.Queued:
                embed.AddField("Song added to queue", Util.FormattedLinkedVideo(playReturnValue.Video));
                break;
            case AudioPlayState.QueuedAsPlaylist:
                embed.WithTitle($"Added {playReturnValue.Videos?.Length} songs to queue");
                break;
            case AudioPlayState.InvalidQuery:
                embed.AddField("Query invalid", "`Couldn't find any results`");
                break;
            case AudioPlayState.NoVoiceChannel:
                embed.AddField("No voice channel", "`Please connect to a voice channel first!`");
                break;
            case AudioPlayState.TooLong:
                embed.AddField("Invalid query", "Song is too long (can't be longer than 1 hour)");
                break;
            case AudioPlayState.JoiningChannelFailed:
                embed.AddField("Couldn't join voice channel",
                    "`Try checking the channels user limit and the bots permission.`");
                break;
            case AudioPlayState.DifferentVoiceChannels:
                embed.AddField("Invalid voice channel",
                    $"You have to be connect to the same voice channel `{playReturnValue.Note}` as the bot.");
                break;
            case AudioPlayState.CancelledEarly:
                embed.AddField("Cancelled", "`Playing was stopped early.`");
                break;
        }

        return new FormattedMessage(embed);
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