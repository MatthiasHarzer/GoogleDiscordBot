using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;
using GoogleBot.Services;


namespace GoogleBot.Interactions.Modules;

[MessageCommandsModule]
public class MessageCommands : CommandModuleBase
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
        await ReplyAsync(Responses.FromPlayReturnValue(returnValue));
    }
}