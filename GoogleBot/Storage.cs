using System.IO;
using System.Reflection;
using GoogleBot.Services;

namespace GoogleBot;

/// <summary>
/// Provides paths for command file and guild config file
/// </summary>
public static class Storage
{
    
    
    /// <summary>
    /// The absolute path of the runtime directory. All local files will be placed here, or in a sub directory
    /// </summary>
    public static string RuntimeDir => Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
    
    /// <summary>
    /// The absolute directory for a json file to save the commands to
    /// </summary>
    public static string CommandsFile => $"{RuntimeDir}/commands.json";

    /// <summary>
    /// The file path for a guild specific configuration
    /// </summary>
    /// <param name="guild">The guild</param>
    /// <returns>The absolute path to the guilds config file</returns>
    public static string GetGuildConfigFileOf(GuildConfig guild)
    {
        if (!Directory.Exists($"{RuntimeDir}/guild.configs"))
        {
            Directory.CreateDirectory($"{RuntimeDir}/guild.configs");
        }

        return $"{RuntimeDir}/guild.configs/guild-{guild.Id}.json";
    }
}

