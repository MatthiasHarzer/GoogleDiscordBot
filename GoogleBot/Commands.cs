using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Google.Apis.CustomSearchAPI.v1.Data;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using static GoogleBot.Util;

namespace GoogleBot
{
    
    public class InfoModule : ModuleBase<SocketCommandContext>
    {
        [Command("echo")]
        [Summary("Echoes a message.")]
        public Task Echo([Summary("message")] params string[] text)
        {
            return ReplyAsync(String.Join(' ', text));
        }

        [Command("help")]
        [Alias("?")]
        [Summary("Shows this dialog")]
        public async Task Help()
        {
            
            EmbedBuilder embed = CommandExecutor.Help();
            

            await ReplyAsync(embed: embed.Build());
        }
    }

    public class GoogleModule : ModuleBase<SocketCommandContext>
    {
        [Command("google")]
        [Alias("gl")]
        [Summary("Google something")]
        public async Task Request([Summary("query")] params string[] query)
        {
            var typing = Context.Channel.EnterTypingState(); //* Start typing animation
            
            EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From("google", Context), query);
            

            // List<Result> results = Actions.FetchGoogleQuery(String.Join(' ', query));
            // results.ForEach(item =>
            // {
            //     // Console.WriteLine(item.Snippet + " " + item.DisplayLink);
            // });


            await ReplyAsync(embed: embed.Build());
            typing.Dispose();
            
        }
    }

    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("Plays audio from an url")]
        public async Task Play([Summary("query")] params string[] query)
        {
            string q = String.Join(" ", query);
            var typing = Context.Channel.EnterTypingState(); //* Start typing animation
            
            EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context), q);

            await ReplyAsync(embed: embed.Build());
            typing.Dispose();
        }


        [Command("skip")]
        [Alias("s")]
        [Summary("Skips current song")]
        public async Task Skip()
        {
            EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
            await ReplyAsync(embed: embed.Build());
        }


        [Command("queue")]
        [Alias("q", "list")]
        [Summary("Shows current queue")]
        public async Task ListQueue()
        {
            EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
            await ReplyAsync(embed: embed.Build());
        }

        [Command("clear")]
        [Alias("c")]
        [Summary("Clears the queue")]
        
        public async Task ClearQueue()
        {
            EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
            await ReplyAsync(embed: embed.Build());
        }


        [Command("stop")]
        [Alias("disconnect", "stfu", "leave")]
        [Summary("Disconnects the bot from the current voice channel")]
        public async  Task Disconnect()
        {
            Console.WriteLine(GetCommandFromMessage(Context.Message));
            
            EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
            await ReplyAsync(embed: embed.Build());
        }
    }
}