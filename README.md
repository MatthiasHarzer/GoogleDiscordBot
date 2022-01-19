# Google Discord Bot
[![.NET](https://github.com/MatthiasHarzer/GoogleDiscordBot/actions/workflows/dotnet.yml/badge.svg)](https://github.com/MatthiasHarzer/GoogleDiscordBot/actions/workflows/dotnet.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
<br>
A Discord bot that can google things and play music, built with [DiscordNET](https://discordnet.dev/).

## Todo for own use:
Configure the [`Secrets.cs`](/GoogleBot/Secrets.cs) file to your needs with the DiscordToken and Api keys or set the environmnet variables accordingly:
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
<br>
<br>

MIT License

Copyright (c) 2022 Matthias Harzer

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
