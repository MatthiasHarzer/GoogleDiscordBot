﻿using System;
using System.Collections.Generic;
using Discord;
using GoogleBot.Interactions.Commands;
using GoogleBot.Interactions.Context;
using GoogleBot.Services;
using YoutubeExplode.Videos;

namespace GoogleBot.Interactions;

public static class Responses
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
    public static FormattedMessage Skipped(Video? nextSong)
    {
        return nextSong == null
            ? new FormattedMessage(new EmbedBuilder().AddField("Skipping", $"The queue is empty -> disconnecting"))
            : new FormattedMessage(new EmbedBuilder().AddField("Skipping",
                $"Now playing {Util.FormattedLinkedVideo(nextSong)}"));
    }

    public static FormattedMessage FromPlayReturnValue(PlayReturnValue playReturnValue)
    {
        EmbedBuilder embed = new EmbedBuilder();
        switch (playReturnValue.AudioPlayState)
        {
            case AudioPlayState.Success:
                embed.AddField("Now playing", Util.FormattedLinkedVideo(playReturnValue.Video!));
                break;
            case AudioPlayState.PlayingAsPlaylist:
                embed.WithTitle($"Added {playReturnValue.Videos.Length} songs to queue");
                embed.AddField("Now playing", Util.FormattedLinkedVideo(playReturnValue.Video!));
                break;
            case AudioPlayState.Queued:
                embed.AddField("Song added to queue", Util.FormattedLinkedVideo(playReturnValue.Video!));
                break;
            case AudioPlayState.QueuedAsPlaylist:
                embed.WithTitle($"Added {playReturnValue.Videos.Length} songs to queue");
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
            case AudioPlayState.VoiceChannelEmpty:
                embed.AddField("Cancelled", "`Voice channel is empty.`");
                break;
            case AudioPlayState.QueuedFirst:
                embed.AddField("Song added", $"{Util.FormattedLinkedVideo(playReturnValue.Video!)} will play next.");
                break;
            case AudioPlayState.OnlyNonPlaylistAllowed:
                embed.AddField("Query invalid", "`Only non playlist links allowed!`");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return new FormattedMessage(embed);
    }

    public static FormattedMessage NothingToSkip()
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Nothing to skip", "The queue is empty."));
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

    public static FormattedMessage WrongVoiceChannel(IVoiceChannel targetVc)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Invalid voice channel",
            $"You have to be connect to {targetVc.Mention} to perform this action."));
    }

    public static FormattedMessage CommandRequiresMajorityEphemeralHint(CommandInfo commandInfo)
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
            $"The command `/{commandInfo.Name}` requires the consent of the majority of members in your voice channel to execute",
            "Consider removing the `hidden` option from the command to trigger a vote"));
    }

    public static FormattedMessage AutoPlayState(bool autoPlay)
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
            "Autoplay:",
            $"Autoplay is currently `{(autoPlay ? "enabled" : "disabled")}`."
        ));
    }
    public static FormattedMessage AutoPlayStateChange(bool newAutoPlay)
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
            "Autoplay:",
            $"Autoplay is now `{(newAutoPlay ? "enabled" : "disabled")}`."
        ));
    }

    public static FormattedMessage QueuePage(AudioPlayer player, int page)
    {
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
        Video? currentSong = player.CurrentSong;

        List<Video> queue = player.Queue;

        if (player.Playing && currentSong != null)
        {
            embed.AddField("Currently playing", $"[`{Util.FormattedVideo(currentSong)}`]({currentSong.Url})");
        }

        if (queue.Count > 0)
        {
            embed.AddField($"Queue ({queue.Count})", player.QueuePages[page]);
        }
        else
        {
            embed.AddField("Queue is empty", "Nothing to show.");
            if(player.AutoPlayEnabled && player.NextTargetSong != null)
            {
                embed.AddField("Autoplay:", Util.FormattedLinkedVideo(player.NextTargetSong));
            }
        }

        string footer = $"Page {page+1}/{Math.Max(player.QueuePages.Length, 1)}";
        if (!player.QueueComplete)
        {
            footer += " - List might be incomplete due to processing playlist songs";
        }
        embed.WithFooter(footer);

        return new FormattedMessage(embed);
    }

    
    public static FormattedMessage VoteRequired(IGuildUser author, string command, int numberOfVotesRequired, int timeout)
    {
        double rounded = Math.Round(timeout * 100f / 60) / 100;
        string end = timeout >= 60 ? $"{rounded} minute{(timeout == 60 ? "" : "s")}" : $"{timeout} seconds";
        
        return new FormattedMessage(new EmbedBuilder().AddField(
            $"`{numberOfVotesRequired}` more {(numberOfVotesRequired == 1 ? "vote" : "votes")} required",
            $"<@{author.Id}> wants to execute `{command}`").WithFooter($"Vote ends in {end}"));
    }
    
    public static FormattedMessage VoteDisabled(ICommandContext context)
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
                $"`/{context.CommandInfo.Name}` requires the majority of the voice channel!",
                $"But voting is disabled for this command!"
        ));
    }

    public static FormattedMessage VoteTimedOut(ICommandContext context)
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
            $"Vote failed. Timed out!",
            $"{context.User.Mention} wanted to execute `{Util.FormattedUsedCommand(context)}`"));
    }

    public static FormattedMessage AlreadyVoted()
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
            "`You already voted!`",
            "You can only vote once."));
    }

    public static FormattedMessage QueueCleared(List<Video> queue)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Queue cleared", $"Removed `{queue.Count}` " +
            (queue.Count == 1 ? "item" : "items") + "."));
    }

    public static FormattedMessage BotsVcNotEmpty(IVoiceChannel vc)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Couldn't perform action",
            $"The bots voice channel {vc.Mention} must be empty!"));
    }

    public static FormattedMessage JoinedVoiceChannel(IVoiceChannel vc)
    {
        return new FormattedMessage(new EmbedBuilder().AddField(
            "Joined voice channel", $"Successfully joined {vc.Mention}"));
    }
}