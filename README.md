# Google Discord Bot
A Discord bot that can google things and play music, built with [DiscordNET](https://discordnet.dev/).

## Todo for own use:
Configure the [`Secrets.cs`](/GoogleBot/Secrets.cs) file to your need with the DiscordToken and Api keys or set the environmnet variables accordingly:
```cs
namespace GoogleBot
{
    public static class Secrets
    {
        public static readonly string DiscordToken = @"YOUR-DISCORD-TOKEN";
        public static readonly string GoogleApiKey = @"YOUR-GOOGLE-CLOUD-API-KEY";
        public static readonly string SearchEngineID = @"YOUR-CUSTOM-SEARCHENGINE-ID";
    }
}
```
