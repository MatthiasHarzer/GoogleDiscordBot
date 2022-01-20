using System;
using YoutubeExplode.Videos;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.Services;
using GoogleBot.Interactions;
using DiscordColor = Discord.Color;
using ParameterInfo = GoogleBot.Interactions.ParameterInfo;
using CommandInfo = GoogleBot.Interactions.CommandInfo;

// using CommandInfo = Discord.Commands.CommandInfo;

namespace GoogleBot
{
    public class HSL
    {
        public double Hue{get; init; }
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
                            else
                            if (t[i] * 2.0 < 1.0)
                                t[i] = q;
                            else
                            if (t[i] * 3.0 < 2.0)
                                t[i] = p + ((q - p) * 6 * ((2.0 / 3.0) - t[i]));
                            else
                                t[i] = p;
                        }
                    }
                }
                catch (Exception ee)
                {
                    
                }

                return Color.FromArgb((int)(t[0] * 255), (int)(t[1] * 255), (int)(t[2] * 255));
            }
        }

    }
    public enum CommandConversionState
    {
        Success,
        Failed,
        NotFound,
        MissingArg,
        InvalidArgType,
        SlashCommandExecutedAsTextCommand,
    }

    
    /// <summary>
    /// Some utility function 
    /// </summary>
    public static class Util
    {
        private static List<Color> color_pallat = null;
        private static readonly Random Random = new Random();
        public static  List<Color> ColorPallet
        {
            get
            {
                if (color_pallat == null)
                {
                    color_pallat = GenerateColorPallet();
                }

                return color_pallat;
            }
        }

        /// <summary>
        /// Generate a nice color pallet. See <a href="http://devmag.org.za/2012/07/29/how-to-choose-colours-procedurally-algorithms/">How to Choose Colours Procedurally (Algorithms)</a>
        /// </summary>
        /// <returns></returns>
        private static List<Color> GenerateColorPallet()
        {
            List<Color> colors = new List<Color>();

            double[] saturations = {1.0f, 0.7f};
            double[] luminances = {0.45f, 0.7f};

            int v = Random.Next(2);
            double saturation = saturations[v];
            double luminance = luminances[v];

            double goldenRatioConjugate = 0.618033988749895f;
            double currentHue = Random.NextDouble();

            int colorCount = 50;
			
            for (int i = 0; i < colorCount; i++)
            {
                HSL hslColor = new HSL{
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
        public static DiscordColor RandomColor(){
            Color[] colors = new Color[3];
         
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = ColorPallet[Random.Next(ColorPallet.Count)];
            }

            //* Mix the colors for even more randomnes
            Color color = RandomMix(colors[0], colors[1], colors[2]);
            return new DiscordColor(color.R, color.G, color.B);
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
            return $"[{video.Title} ({FormattedVideoDuration(video)})]({video.Url})";
        }

        /// <summary>
        /// Get the used command and given arguments from the message
        /// </summary>
        /// <param name="message">The message provided from the Discord API</param>
        /// <returns>The command and an array including the arguments</returns>
        public static CommandConversionInfo GetCommandInfoFromMessage(SocketUserMessage message)
        {
            int argPos = 0;
            if (message.ToString().Length > 1 && message.HasCharPrefix('!', ref argPos))
            {
                string raw_command = message.ToString().Split(" ")[0].Substring(argPos);

                foreach (CommandInfo ctx in CommandMaster.CommandsList)
                {
                    if (ctx.Name.Equals(raw_command) || ctx.Aliases.Contains(raw_command))
                    {
                        string command = ctx.Name;
                        string[] raw_args = message.ToString().Substring(argPos + raw_command.Length).Split(" ")
                            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                        List<object> args = new List<object>();

                        int index = 0;

                        List<ParameterInfo> missingArgs = new();

                        //* Check if all required params are present
                        foreach (ParameterInfo param in ctx.Parameters)
                        {
                            // Console.WriteLine(param);
                            //* Prevent OutOfBounds Exception
                            if (index < raw_args.Length)
                            {
                                List<(string, Type)> wrongTypes = new();

                                if (!param.IsMultiple)
                                {
                                    try
                                    {
                                        object c = Convert.ChangeType(raw_args[index], param.Type);

                                        if (c != null)
                                        {
                                            args.Add(c);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e.StackTrace);
                                        wrongTypes.Add((raw_args[index], param.Type));
                                    }
                                }
                                else
                                {
                                    List<dynamic> multiple = new();
                                    //* Get all additional params if it has multiple words
                                    for (int i = index; i < raw_args.Length; i++)
                                    {
                                        try
                                        {
                                            dynamic c = Convert.ChangeType(raw_args[i], param.Type);

                                            if (c != null)
                                            {
                                                multiple.Add(c);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(e.StackTrace);
                                            wrongTypes.Add((raw_args[i], param.Type));
                                        }
                                    }

                                    //* Convert dynamic array to explicit array of type <param.Type>
                                    //* I dont understand what the heck heppens here, but it works
                                    //* ->https://stackoverflow.com/a/51654220/11664234
                                    var typeConvertedEnumerable = typeof(System.Linq.Enumerable)
                                        .GetMethod("Cast", BindingFlags.Static | BindingFlags.Public)
                                        ?.MakeGenericMethod(new Type[] { param.Type })
                                        .Invoke(null, new object[] { multiple.ToArray() });
                                    var typeConvertedArray = typeof(System.Linq.Enumerable)
                                        .GetMethod("ToArray", BindingFlags.Static | BindingFlags.Public)
                                        ?.MakeGenericMethod(new Type[] { param.Type })
                                        .Invoke(null, new object[] { typeConvertedEnumerable });
                                    args.Add(typeConvertedArray);
                                }


                                if (wrongTypes.Count > 0)
                                {
                                    return new CommandConversionInfo
                                    {
                                        State = CommandConversionState.InvalidArgType,
                                        Command = CommandMaster.GetCommandFromName(command),
                                        TargetTypeParam = wrongTypes.ToArray()
                                    };
                                }
                            }
                            else
                            {
                                if (!param.IsOptional)
                                {
                                    missingArgs.Add(param);
                                }
                            }

                            index++;
                        }

                        if (missingArgs.Count > 0)
                        {
                            return new CommandConversionInfo
                            {
                                State = CommandConversionState.MissingArg,
                                Command = CommandMaster.GetCommandFromName(command),
                                MissingArgs = missingArgs.ToArray(),
                            };
                        }

                        // Console.WriteLine("Command: " + command);
                        // Console.WriteLine(string.Join(", ", args.ConvertAll(a=>$"({a.GetType()}) {a}")));
                        // Console.WriteLine("-----");
                        return new CommandConversionInfo
                        {
                            State = CommandConversionState.Success,
                            Command = CommandMaster.GetCommandFromName(command),
                            Arguments = args.ToArray(),
                        };
                    }
                }
            }

            return new CommandConversionInfo
            {
                State = CommandConversionState.Failed
            };
        }


        /// <summary>
        /// Primarily used to bring the slash commands options into to for the command executor understandable format  
        /// </summary>
        /// <param name="command">The SocketSlashCommand provided by discord</param>
        /// <returns>Information about the command conversion</returns>
        public static CommandConversionInfo GetCommandInfoFromSlashCommand(SocketSlashCommand command)
        {
            CommandInfo cmd = CommandMaster.GetCommandFromName(command.CommandName);

            if (cmd == null)
            {
                return new CommandConversionInfo
                {
                    State = CommandConversionState.NotFound,
                };
            }

            string[] raw_args = command.Data.Options.ToList().ConvertAll(o =>(string) o.Value).ToArray();

            List<object> args = new List<object>();

            int index = 0;

            foreach (ParameterInfo param in cmd.Parameters)
            {
                if (index < raw_args.Length)
                {
                    List<(string, Type)> wrongTypes = new();

                    if (!param.IsMultiple)
                    {
                        try
                        {
                            object c = Convert.ChangeType(raw_args[index], param.Type);

                            if (c != null)
                            {
                                args.Add(c);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.StackTrace);
                            wrongTypes.Add((raw_args[index], param.Type));
                        }
                    }
                    else
                    {
                        List<dynamic> multiple = new();
                        //* Get all additional params if it has multiple words
                        for (int i = index; i < raw_args.Length; i++)
                        {
                            try
                            {
                                dynamic c = Convert.ChangeType(raw_args[i], param.Type);

                                if (c != null)
                                {
                                    multiple.Add(c);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.StackTrace);
                                wrongTypes.Add((raw_args[i], param.Type));
                            }
                        }

                        //* Convert dynamic array to explicit array of type <param.Type>
                        //* I dont understand what the heck heppens here, but it works
                        //* ->https://stackoverflow.com/a/51654220/11664234
                        var typeConvertedEnumerable = typeof(System.Linq.Enumerable)
                            .GetMethod("Cast", BindingFlags.Static | BindingFlags.Public)
                            ?.MakeGenericMethod(new Type[] { param.Type })
                            .Invoke(null, new object[] { multiple.ToArray() });
                        var typeConvertedArray = typeof(System.Linq.Enumerable)
                            .GetMethod("ToArray", BindingFlags.Static | BindingFlags.Public)
                            ?.MakeGenericMethod(new Type[] { param.Type })
                            .Invoke(null, new object[] { typeConvertedEnumerable });
                        args.Add(typeConvertedArray);
                    }


                    if (wrongTypes.Count > 0)
                    {
                        return new CommandConversionInfo
                        {
                            State = CommandConversionState.InvalidArgType,
                            Command = CommandMaster.GetCommandFromName(cmd.Name) ,
                            TargetTypeParam = wrongTypes.ToArray()
                        };
                    }
                }


                index++;
            }

            return new CommandConversionInfo
            {
                State = CommandConversionState.Success,
                Command = CommandMaster.GetCommandFromName(command.CommandName),
                Arguments = args.ToArray(),
            };
        }
    }
}
