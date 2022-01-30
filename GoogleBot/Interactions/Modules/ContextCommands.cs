using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;

namespace GoogleBot.Interactions.Modules;

public class MessageCommands : MessageCommandsModuleBase
{
    [Command("echo")]
    public async Task Test(SocketMessage message)
    {
        // Console.WriteLine("In Test Command " + message.Embeds.Count+" "  + message.Embeds?.FirstOrDefault()?.ToEmbedBuilder()!);
        await ReplyAsync(new FormattedMessage
        {
            Message = message.Content,
            Embed = message.Embeds?.FirstOrDefault()?.ToEmbedBuilder()!
        });
    }

    [Command("play")]
    public async Task Play(SocketMessage message)
    {
        string query = message.Content;
        IVoiceChannel channel = Context.VoiceChannel!;
        EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();


        if (channel == null)
        {
            embed.AddField("No voice channel", "`Please connect to voice channel first!`");

            await ReplyAsync(embed);
            return;
        }

        AudioPlayer player = Context.GuildConfig.AudioPlayer;
        PlayReturnValue returnValue = await player.Play(query, channel);


        //* User response
        switch (returnValue.AudioPlayState)
        {
            case AudioPlayState.Success:
                embed.AddField("Now playing",
                    Util.FormattedVideo(returnValue.Video));
                break;
            case AudioPlayState.PlayingAsPlaylist:
                embed.WithTitle($"Added {returnValue.Videos?.Length} songs to queue");
                embed.AddField("Now playing",
                    Util.FormattedVideo(returnValue.Video));
                break;
            case AudioPlayState.Queued:
                embed.AddField("Song added to queue",
                    Util.FormattedVideo(returnValue.Video));
                break;
            case AudioPlayState.QueuedAsPlaylist:
                embed.WithTitle($"Added {returnValue.Videos?.Length} songs to queue");
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
                    $"You have to be connect to the same voice channel `{returnValue.Note}` as the bot.");
                break;
            case AudioPlayState.CancelledEarly:
                embed.AddField("Cancelled", "`Playing was stopped early.`");
                break;
        }

        await ReplyAsync(embed);
    }
}