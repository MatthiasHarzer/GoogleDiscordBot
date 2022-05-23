using System.Text.RegularExpressions;
using Discord.WebSocket;

namespace GoogleBot;

/// <summary>
/// A place to store all global cross guild data
/// </summary>
public static class Globals
{
    public static DiscordSocketClient Client { get; set; } = null!;

    //See https://stackoverflow.com/a/61033353/11664234
    public static readonly Regex YoutubeRegex =
        new Regex(@"(?:https?:\/\/)?(?:[a-z]*\.)?youtu(?:\.be\/|be.com\/\S*(?:watch|embed)(?:(?:(?=\/[-a-zA-Z0-9_]{11,}(?!\S))\/)|(?:\S*v=|v\/)))([-a-zA-Z0-9_]{11,})", RegexOptions.IgnoreCase);

    public const string IdleTimerId = @"idle-timer";

}