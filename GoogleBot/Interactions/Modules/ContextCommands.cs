using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;

namespace GoogleBot.Interactions.Modules;

public class MessageCommands : MessageCommandsModuleBase
{
    [Command("echo")]
    public async Task Test(string text)
    {
        Console.WriteLine("In Test Command " + text);
        await ReplyAsync(text);
    }
}