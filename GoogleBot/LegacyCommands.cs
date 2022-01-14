using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Google.Apis.CustomSearchAPI.v1.Data;
using GoogleBot.Interactions;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using static GoogleBot.Util;

namespace GoogleBot
{
    // public class CommandTracker : Attribute
    // {
    //     public CommandTracker()
    //     {
    //         
    //     }
    // }
    //
    // public class Mod : ModuleBase<SocketCommandContext>
    // {
    //     [Discord.Commands.Command("echo")]
    //     [Summary("Echoes a message.")]
    //     public Task Echo([Summary("message")] params string[] text)
    //     {
    //         return ReplyAsync(String.Join(' ', text));
    //     }
    // }
    // public class BotModule : ModuleBase<SocketCommandContext>
    // {
    //     public static ExecuteContext Context { get; set; }
    //     private static readonly Dictionary<ulong, AudioPlayer> guildMaster = new Dictionary<ulong, AudioPlayer>();
    //     
    //     [Command("echo")]
    //     [Summary("Echoes a message.")]
    //     public Task Echo([Summary("message")] params string[] text)
    //     {
    //         return ReplyAsync(String.Join(' ', text));
    //     }
    //
    //     [Command("help")]
    //     [Alias("?")]
    //     [Summary("Shows this dialog")]
    //     public async Task Help()
    //     {
    //         
    //         // EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
    //         
    //
    //         // await ReplyAsync(embed: embed.Build());
    //     }
    //
    //     [Command("test")]
    //     [Summary("Test com")]
    //     public async Task Test(int number, string text, params int[] p)
    //     {
    //         Console.WriteLine("EXECUTED TEST!!!!");
    //         Console.WriteLine($"{number}, {text}, {string.Join(", ", p)}");
    //         // await ReplyAsync(number.ToString());
    //     }
    //
    //     
    //     [Command("google")]
    //     [Alias("gl")]
    //     [Summary("Google something")]
    //     public async Task Request([Summary("query")] params string[] query)
    //     {
    //         var typing = Context.Channel.EnterTypingState(); //* Start typing animation
    //         
    //         // EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context), query);
    //         
    //
    //         // List<Result> results = Actions.FetchGoogleQuery(String.Join(' ', query));
    //         // results.ForEach(item =>
    //         // {
    //         //     // Console.WriteLine(item.Snippet + " " + item.DisplayLink);
    //         // });
    //
    //
    //         // await ReplyAsync(embed: embed.Build());
    //         typing.Dispose();
    //         
    //     }
    //
    //     [Command("play", RunMode = RunMode.Async)]
    //     [Alias("p")]
    //     [Summary("Plays audio from an url")]
    //     public async Task<EmbedBuilder> Play([Summary("query")] params string[] query_)
    //     {
    //         string query = string.Join(" ", query_);
    //         EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
    //         IVoiceChannel channel = Context.VoiceChannel;
    //
    //
    //         if (channel == null)
    //         {
    //             embed.AddField("No voice channel", "`Please connect to voice channel first!`");
    //             return embed;
    //         }
    //
    //
    //         if (!guildMaster.ContainsKey(channel.GuildId))
    //         {
    //             guildMaster.Add(channel.GuildId, new AudioPlayer());
    //         }
    //
    //         AudioPlayer player = guildMaster[channel.GuildId];
    //         IPlayReturnValue returnValue = await player.Play(query, channel);
    //
    //
    //         //* User response
    //         switch (returnValue.AudioPlayState)
    //         {
    //             case AudioPlayState.Success:
    //                 embed.AddField("Now playing",
    //                     FormattedVideo(returnValue.Video));
    //                 break;
    //             case AudioPlayState.PlayingAsPlaylist:
    //                 embed.WithTitle($"Added {returnValue.Videos?.Length} songs to queue");
    //                 embed.AddField("Now playing",
    //                     FormattedVideo(returnValue.Video));
    //                 break;
    //             case AudioPlayState.Queued:
    //                 embed.AddField("Song added to queue",
    //                     FormattedVideo(returnValue.Video));
    //                 break;
    //             case AudioPlayState.QueuedAsPlaylist:
    //                 embed.WithTitle($"Added {returnValue.Videos?.Length} songs to queue");
    //                 break;
    //             case AudioPlayState.InvalidQuery:
    //                 embed.AddField("Query invalid", "`Couldn't find any results`");
    //                 break;
    //             case AudioPlayState.NoVoiceChannel:
    //                 embed.AddField("No voice channel", "`Please connect to voice channel first!`");
    //                 break;
    //             case AudioPlayState.TooLong:
    //                 embed.AddField("Invalid query", "Song is too long (can't be longer than 1 hour)");
    //                 break;
    //         }
    //
    //         return embed;
    //     }
    //
    //
    //     [Command("skip")]
    //     [Alias("s")]
    //     [Summary("Skips current song")]
    //     public async Task Skip()
    //     {
    //         // EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
    //         // await ReplyAsync(embed: embed.Build());
    //     }
    //
    //
    //     [Command("queue")]
    //     [Alias("q", "list")]
    //     [Summary("Shows current queue")]
    //     public async Task ListQueue()
    //     {
    //         // EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
    //         // await ReplyAsync(embed: embed.Build());
    //     }
    //
    //     [Command("clear")]
    //     [Alias("c")]
    //     [Summary("Clears the queue")]
    //     
    //     public async Task ClearQueue()
    //     {
    //         // EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
    //         // await ReplyAsync(embed: embed.Build());
    //     }
    //
    //
    //     [Command("stop")]
    //     [Alias("disconnect", "stfu", "leave")]
    //     [Summary("Disconnects the bot from the current voice channel")]
    //     public async  Task Disconnect()
    //     {
    //         // Console.WriteLine(GetCommandInfoFromMessage(Context.Message));
    //         
    //         // EmbedBuilder embed = await CommandExecutor.Execute(ExecuteContext.From(Context));
    //         // await ReplyAsync(embed: embed.Build());
    //     }
    // }
    //
}
