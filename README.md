# Google Discord Bot
A Discord bot that can google things and play music, built with [DiscordNET](https://discordnet.dev/).

## Todo for own use:
Add a `Secrets.cs` file with your DiscordToken and Api keys:
<sub><sub>Sorry, to lazy to add a dummy file.</sub></sub>
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
