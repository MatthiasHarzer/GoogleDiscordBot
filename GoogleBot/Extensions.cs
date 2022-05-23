using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Discord.WebSocket;
using GoogleBot.Services;

namespace GoogleBot;


public static class Extensions
{
    /// <summary>
    /// Gets the enum description or if not set, its name
    /// </summary>
    /// <param name="e">The enum entry</param>
    /// <returns>The description or name</returns>
    public static string GetDescription(this Enum e)
    {
        var attribute =
            e.GetType()
                    .GetTypeInfo()
                    .GetMember(e.ToString())
                    .FirstOrDefault(member => member.MemberType == MemberTypes.Field)
                    ?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .SingleOrDefault()
                as DescriptionAttribute;

        return attribute?.Description ?? e.ToString();
    }

    /// <summary>
    /// Gets the enum value
    /// </summary>
    /// <param name="e">The enum entry</param>
    /// <returns>The integer value</returns>
    public static int ToInt(this Enum e)
    {
        unchecked
        {
            if (e.GetTypeCode() == TypeCode.UInt64)
                return (int)Convert.ToUInt64(e);
            return (int)Convert.ToInt64(e);
        }
    }

    public static GuildConfig GetGuildConfig(this SocketGuild guild)
    {
        return Services.GuildConfig.Get(guild);
    }
}