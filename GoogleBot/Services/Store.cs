
using System.Collections.Generic;

namespace GoogleBot.Services;
/// <summary>
/// A data store for one <see cref="GuildConfig"/>
/// </summary>
public class Store
{
    /// <summary>
    /// A general purpose key-value store
    /// </summary>
    public Dictionary<string, dynamic> Raw { get; set; } = new Dictionary<string, dynamic>();

    public int QueuePage { get; set; } = 0;

    public long LastPlayedTs { get; set; } = Util.TimestampNow;

}