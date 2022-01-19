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
using ParameterInfo = GoogleBot.Interactions.ParameterInfo;
using CommandInfo = GoogleBot.Interactions.CommandInfo;

// using CommandInfo = Discord.Commands.CommandInfo;

namespace GoogleBot
{
    public enum CommandConversionState
    {
        Success,
        Failed,
        NotFound,
        MissingArg,
        InvalidArgType,
    }

    /// <summary>
    /// Simple return value for converting a message into its parts (command, args)
    /// </summary>
    public class CommandConversionInfo
    {
        public string Command { get; set; }
        public object[] Arguments { get; set; }
        public CommandConversionState State { get; set; }

        public (string, Type)[] TargetTypeParam { get; set; }


        public ParameterInfo[] MissingArgs { get; set; }
    }

    /// <summary>
    /// Some utility function 
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// Returns a random color that should be not too distracting. See <a  href="http://blog.functionalfun.net/2008/07/random-pastel-colour-generator.html">Random Pastel Colour Generator</a >
        /// </summary>
        /// <returns>The newly generated color</returns>
        public static Color RandomColor(){
            
            Random random = new Random();

            byte[] colorBytes = new byte[3];
            colorBytes[0] = (byte)(random.Next(128) + 127);
            colorBytes[1] = (byte)(random.Next(128) + 127);
            colorBytes[2] = (byte)(random.Next(128) + 127);

            return new Color(colorBytes[0], colorBytes[1], colorBytes[2]);
            
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
                                        Command = command,
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
                                Command = command,
                                MissingArgs = missingArgs.ToArray(),
                            };
                        }

                        // Console.WriteLine("Command: " + command);
                        // Console.WriteLine(string.Join(", ", args.ConvertAll(a=>$"({a.GetType()}) {a}")));
                        // Console.WriteLine("-----");
                        return new CommandConversionInfo
                        {
                            State = CommandConversionState.Success,
                            Command = command,
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
                            Command = cmd.Name,
                            TargetTypeParam = wrongTypes.ToArray()
                        };
                    }
                }


                index++;
            }

            return new CommandConversionInfo
            {
                State = CommandConversionState.Success,
                Command = command.CommandName,
                Arguments = args.ToArray(),
            };
        }
    }
}