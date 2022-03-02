using System;

namespace GoogleBot;

public static class Secrets
{
    public static readonly string DiscordToken = Environment.GetEnvironmentVariable("DiscordToken") ?? string.Empty;
    public static readonly string GoogleApiKey = Environment.GetEnvironmentVariable("GoogleApiKey") ?? string.Empty;
    public static readonly string SearchEngineId = Environment.GetEnvironmentVariable("SearchEngineID") ?? string.Empty;
    public static readonly ulong DevGuildId = Convert.ToUInt64(Environment.GetEnvironmentVariable("DevGuildID"));
}