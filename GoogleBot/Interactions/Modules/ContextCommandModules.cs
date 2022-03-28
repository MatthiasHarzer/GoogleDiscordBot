﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.Preconditions;
using GoogleBot.Services;

namespace GoogleBot.Interactions.Modules;

public class MessageCommands : MessageCommandModuleBase
{
    [Command("echo")]
    public async Task Test(SocketMessage message)
    {
        // Console.WriteLine("In Test Command " + message.Embeds.Count+" "  + message.Embeds?.FirstOrDefault()?.ToEmbedBuilder()!);
        try
        {
            
            await ReplyAsync(new FormattedMessage
            {
                Message = message.Content,
                Embed = message.Embeds?.FirstOrDefault()?.ToEmbedBuilder()!
            });
        }
        catch (Exception)
        {
            if (string.IsNullOrWhiteSpace(message.Content) || string.IsNullOrEmpty(message.Content))
            {
                await ReplyAsync("`Couldn't echo message`");
                await (await Context.Respondable.GetOriginalResponseAsync()).DeleteAsync();
            }
            else
            {
                await ReplyAsync(new FormattedMessage
                {
                    Message = message.Content,
                });
            }
        }
    }

    [Command("play")]
    [RequiresSameVoiceChannel]
    [RequiresConnectedToVoiceChannel]
    public async Task Play(SocketMessage message)
    {
        string query = message.Content;
        IVoiceChannel channel = Context.VoiceChannel!;

        AudioPlayer player = Context.GuildConfig.AudioPlayer;
        PlayReturnValue returnValue = await player.Play(query, channel);


        //* User response
        await ReplyAsync(Responses.FromPlayReturnValue(returnValue));
    }
}