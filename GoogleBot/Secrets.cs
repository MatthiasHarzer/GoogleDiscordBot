using System;

namespace GoogleBot
{
    public static class Secrets
    {
        public static readonly string DiscordToken = Environment.GetEnvironmentVariable("DiscordToken");
        public static readonly string GoogleApiKey = Environment.GetEnvironmentVariable("GoogleApiKey");
        public static readonly string SearchEngineID = Environment.GetEnvironmentVariable("SearchEngineID");
    }
}