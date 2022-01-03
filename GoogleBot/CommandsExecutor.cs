using System;
using Discord;
using YoutubeExplode.Videos;
using static GoogleBot.Globals;

namespace GoogleBot;

// public static class CommandsExecutor
// {
//     
//     public static EmbedBuilder Play(IVoiceChannel channel, string query)
//     {
//       
//         EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
//
//
//             if (channel == null)
//             {
//                 embed.AddField("No voice channel", "`Please connect to voice channel first!`");
//                 return embed;
//             }
//
//            
//             if (!guildMaster.ContainsKey(channel.GuildId))
//             {
//                 guildMaster.Add(channel.GuildId, new AudioPlayer());
//             }
//
//             AudioPlayer player = guildMaster[channel.GuildId];
//             (State state, Video video) = await player.Play(query, channel);
//
//             EmbedBuilder embed = new EmbedBuilder().WithCurrentTimestamp();
//
//             //* User response
//             switch (state)
//             {
//                 case State.Success:
//                     embed.AddField("Now playing",
//                         FormattedVideo(video));
//                     break;
//                 case State.PlayingAsPlaylist:
//                     embed.AddField("Added Playlist to queue", "⠀");
//                     embed.AddField("Now playing",
//                         FormattedVideo(video));
//                     break;
//                 case State.Queued:
//                     embed.AddField("Song added to queue",
//                         FormattedVideo(video));
//                     break;
//                 case State.QueuedAsPlaylist:
//                     embed.AddField("Playlist added to queue", "⠀");
//                     break;
//                 case State.InvalidQuery:
//                     embed.AddField("Query invalid", "`Couldn't find any results`");
//                     break;
//                 case State.NoVoiceChannel:
//                     embed.AddField("No voice channel", "`Please connect to voice channel first!`");
//                     break;
//                 case State.TooLong:
//                     embed.AddField("Invalid query", "Song is too long (can't be longer than 1 hour)");
//                     break;
//             }
//
//             await ReplyAsync(embed: embed.Build());
//             typing.Dispose();
//     }
// }