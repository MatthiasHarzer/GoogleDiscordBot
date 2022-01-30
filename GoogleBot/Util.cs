using System;
using YoutubeExplode.Videos;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.Services;
using GoogleBot.Interactions;
using GoogleBot.Interactions.Commands;
using Color = System.Drawing.Color;
using CommandInfo = GoogleBot.Interactions.Commands.CommandInfo;
using DiscordColor = Discord.Color;
using ParameterInfo = GoogleBot.Interactions.Commands.ParameterInfo;

// using CommandInfo = Discord.Commands.CommandInfo;

namespace GoogleBot;

public class HSL
{
    public double Hue { get; init; }
    public double Saturation { get; init; }
    public double Brightness { get; init; }

    /// <summary>
    /// <para>Convert from the current HSL to RGB</para>
    /// <para>http://en.wikipedia.org/wiki/HSV_color_space#Conversion_from_HSL_to_RGB</para>
    /// </summary>
    public Color Color
    {
        get
        {
            double[] t = new double[] { 0, 0, 0 };

            try
            {
                double tH = Hue;
                double tS = Saturation;
                double tL = Brightness;

                if (tS.Equals(0))
                {
                    t[0] = t[1] = t[2] = tL;
                }
                else
                {
                    double q, p;

                    q = tL < 0.5 ? tL * (1 + tS) : tL + tS - (tL * tS);
                    p = 2 * tL - q;

                    t[0] = tH + (1.0 / 3.0);
                    t[1] = tH;
                    t[2] = tH - (1.0 / 3.0);

                    for (byte i = 0; i < 3; i++)
                    {
                        t[i] = t[i] < 0 ? t[i] + 1.0 : t[i] > 1 ? t[i] - 1.0 : t[i];

                        if (t[i] * 6.0 < 1.0)
                            t[i] = p + ((q - p) * 6 * t[i]);
                        else if (t[i] * 2.0 < 1.0)
                            t[i] = q;
                        else if (t[i] * 3.0 < 2.0)
                            t[i] = p + ((q - p) * 6 * ((2.0 / 3.0) - t[i]));
                        else
                            t[i] = p;
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return Color.FromArgb((int)(t[0] * 255), (int)(t[1] * 255), (int)(t[2] * 255));
        }
    }
}

/// <summary>
/// Some utility function 
/// </summary>
public static class Util
{
    private static List<Color> colorPallat = null;
    private static readonly Random Random = new Random();

    private static List<Color> ColorPallet
    {
        get
        {
            if (colorPallat == null)
            {
                colorPallat = GenerateColorPallet();
            }

            return colorPallat;
        }
    }

    /// <summary>
    /// Generate a nice color pallet. See <a href="http://devmag.org.za/2012/07/29/how-to-choose-colours-procedurally-algorithms/">How to Choose Colours Procedurally (Algorithms)</a>
    /// </summary>
    /// <returns></returns>
    private static List<Color> GenerateColorPallet()
    {
        List<Color> colors = new List<Color>();

        double[] saturations = { 1.0f, 0.7f };
        double[] luminances = { 0.45f, 0.7f };

        int v = Random.Next(2);
        double saturation = saturations[v];
        double luminance = luminances[v];

        double goldenRatioConjugate = 0.618033988749895f;
        double currentHue = Random.NextDouble();

        int colorCount = 50;

        for (int i = 0; i < colorCount; i++)
        {
            HSL hslColor = new HSL
            {
                Hue = currentHue,
                Saturation = saturation,
                Brightness = luminance
            };

            colors.Add(hslColor.Color);

            currentHue += goldenRatioConjugate;
            currentHue %= 1.0f;
        }

        return colors;
    }

    private static Color RandomMix(Color color1, Color color2, Color color3)
    {
        double[] greys = { 0.1, 0.5, 0.9 };

        double grey = greys[Random.Next(greys.Length)];

        int randomIndex = Random.Next(3);

        double mixRatio1 =
            (randomIndex == 0) ? Random.NextDouble() * grey : Random.NextDouble();

        double mixRatio2 =
            (randomIndex == 1) ? Random.NextDouble() * grey : Random.NextDouble();

        double mixRatio3 =
            (randomIndex == 2) ? Random.NextDouble() * grey : Random.NextDouble();

        double sum = mixRatio1 + mixRatio2 + mixRatio3;

        mixRatio1 /= sum;
        mixRatio2 /= sum;
        mixRatio3 /= sum;

        return Color.FromArgb(
            255,
            (byte)(mixRatio1 * color1.R + mixRatio2 * color2.R + mixRatio3 * color3.R),
            (byte)(mixRatio1 * color1.G + mixRatio2 * color2.G + mixRatio3 * color3.G),
            (byte)(mixRatio1 * color1.B + mixRatio2 * color2.B + mixRatio3 * color3.B));
    }

    /// <summary>
    /// Returns a random color based on the initial generated color pallet
    /// </summary>
    /// <returns>The newly generated color</returns>
    public static DiscordColor RandomColor()
    {
        Color[] colors = new Color[3];

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = ColorPallet[Random.Next(ColorPallet.Count)];
        }

        //* Mix the colors for even more randomnes
        Color color = RandomMix(colors[0], colors[1], colors[2]);
        return new DiscordColor(color.R, color.G, color.B);
    }

    public static string RandomString(int length = 20)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }

    public static int RandomNumber(int max = 100)
    {
        return Random.Next(max);
    }


    /// <summary>
    /// Fetch data from the Google custom search api
    /// </summary>
    /// <param name="query">The query / searchterm</param>
    /// <returns>Searchresult</returns>
    public static Search FetchGoogleQuery(string query)
    {
        CustomSearchAPIService service = new CustomSearchAPIService(new BaseClientService.Initializer
        {
            // ApplicationName = "Google Bot (Discord)",
            ApiKey = Secrets.GoogleApiKey
        });

        var listRequest = service.Cse.List();
        listRequest.Cx = Secrets.SearchEngineID;
        listRequest.Q = query;

        listRequest.Start = 10;

        return listRequest.Execute();
    }

    /// <summary>
    /// Format a Videos duration (strip of hours if they are 0)
    /// </summary>
    /// <param name="video">The Video to get the duration from</param>
    /// <returns>Formatted video duration</returns>
    public static string FormattedVideoDuration(Video video)
    {
        if (video.Duration.Value.Hours == 0)
        {
            return
                $"{video.Duration.Value.Minutes.ToString().PadLeft(2, '0')}:{video.Duration.Value.Seconds.ToString().PadLeft(2, '0')}";
        }
        else
        {
            return video.Duration.ToString();
        }
    }

    /// <summary>
    /// Formats a video to a unified string
    /// </summary>
    /// <param name="video">The video</param>
    /// <returns></returns>
    public static string FormattedVideo(Video video)
    {
        return $"{video.Title} - {video.Author.Title} ({FormattedVideoDuration(video)})";
    }

    public static string FormattedLinkedVideo(Video video)
    {
        return $"[`{FormattedVideo(video)}`]({video.Url})";
    }

    /// <summary>
    /// Formats a command to a usage hint when needed
    /// </summary>
    /// <param name="command">The command to format</param>
    /// <returns>The formatted command as a string including optional params</returns>
    public static string FormattedCommand(CommandInfo command)
    {
        return
            $"/{command.Name}{(command.Parameters.Length > 0 ? " " : "")}{String.Join(" ", command.Parameters.AsParallel().ToList().ConvertAll(p => p.IsOptional ? $"[<{p.Name}>]" : $"<{p.Name}>"))}{(command.IsOptionalEphemeral ? " [<hidden>]" : "")}";
    }

    public static CommandInfo GetTextCommandFromMessage(SocketUserMessage message)
    {
        int argPos = 0;
        if (message.ToString().Length > 1 && message.HasCharPrefix('!', ref argPos))
        {
            string raw_command = message.ToString().Split(" ")[0].Substring(argPos);

            foreach (CommandInfo ctx in InteractionMaster.CommandList)
            {
                if (ctx.Name.Equals(raw_command))
                {
                    return ctx;
                }
            }
        }

        return new CommandInfo();
    }

    public static ApplicationCommandOptionType ToOptionType(Type origin)
    {
        switch (origin)
        {
            case Type _ when origin == typeof(string):
                return ApplicationCommandOptionType.String;
            case Type _ when origin == typeof(bool):
                return ApplicationCommandOptionType.Boolean;
            case Type _ when origin == typeof(int):
                return ApplicationCommandOptionType.Integer;
            case Type _ when origin == typeof(float):
            case Type _ when origin == typeof(double):
                return ApplicationCommandOptionType.Number;
            case Type _ when origin == typeof(SocketGuildUser):
            case Type _ when origin == typeof(SocketUser):
                return ApplicationCommandOptionType.User;
            case Type _ when origin == typeof(SocketRole):
                return ApplicationCommandOptionType.Role;
            case Type _ when origin == typeof(SocketChannel):
                return ApplicationCommandOptionType.Channel;
            default:
                return ApplicationCommandOptionType.String;
        }
    }

    public static ApplicationCommandOptionType ToOptionType(string type)
    {
        switch (type)
        {
            case "string":
                return ApplicationCommandOptionType.String;
            case "boolean":
                return ApplicationCommandOptionType.Boolean;
            case "integer":
                return ApplicationCommandOptionType.Integer;
            case "number":
                return ApplicationCommandOptionType.Number;
            case "user":
                return ApplicationCommandOptionType.User;
            case "role":
                return ApplicationCommandOptionType.Role;
            case "channel":
                return ApplicationCommandOptionType.Channel;
            case "mentionable":
                return ApplicationCommandOptionType.Mentionable;
            default:
                return ApplicationCommandOptionType.String;
        }
    }

    public static string OptionTypeToString(ApplicationCommandOptionType type)
    {
        switch (type)
        {
            case ApplicationCommandOptionType.String:
                return "string";
            case ApplicationCommandOptionType.Boolean:
                return "boolean";
            case ApplicationCommandOptionType.Integer:
                return "integer";
            case ApplicationCommandOptionType.Number:
                return "number";
            case ApplicationCommandOptionType.User:
                return "user";
            case ApplicationCommandOptionType.Role:
                return "role";
            case ApplicationCommandOptionType.Channel:
                return "channel";
            default:
                return "string";
        }
    }
    

    public static bool ApproximatelyEqual(CommandInfo command1, CommandInfo command2)
    {
        if (command1.Name != command2.Name || command1.Summary != command2.Summary ||
            command1.Parameters.Length != command2.Parameters.Length ||
            command1.IsDevOnly != command2.IsDevOnly ||
            command1.IsOptionalEphemeral != command2?.IsOptionalEphemeral) return false;

        int pos = 0;
        for (pos = 0; pos < command1.Parameters.Length; pos++)
        {
            // Console.WriteLine(pos);
            if (pos > command2.Parameters.Length)
            {
                return false;
            }

            ParameterInfo p1 = command1.Parameters[pos];
            ParameterInfo p2 = command2.Parameters[pos];

            if (p1.Name != p2.Name || p1.Summary != p2.Summary || p1.Type != p2.Type ||
                p1.IsOptional != p2.IsOptional) return false;
        }

        if (command2.Parameters.Length > pos) return false;

        return true;
    }
}