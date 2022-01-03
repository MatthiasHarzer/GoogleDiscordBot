# GoogleDiscordBot
Discord Bot that Googles and plays music

## Todo for own use:
Add a `Secrets.cs` file with your DiscordToken and Api keys:
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
