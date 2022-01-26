using Discord;
using static GoogleBot.Util;

namespace GoogleBot.Interactions.Modules;

public class Responses
{
    public static FormattedMessage SkipVote(int numberOfVotesRequired)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Skip vote started",
            $"`{numberOfVotesRequired}` more {(numberOfVotesRequired == 1 ? "vote" : "votes")} required to skip"));
    }

    public static FormattedMessage Skipped(string skippedSong)
    {
        return new FormattedMessage(new EmbedBuilder().AddField("Skipping", $"Song {skippedSong} skipped"));
    }
}