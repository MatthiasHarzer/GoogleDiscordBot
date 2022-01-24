#nullable enable
using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using GoogleBot.Interactions.CustomAttributes;

namespace GoogleBot.Interactions.Modules;


public class TestModule : ApplicationModuleBase
{
    [Command("component-test")]
    public async void Play([Multiple][Summary("multiple word")][Name("input")]string query)
    {
        ComponentBuilder builder = new ComponentBuilder().WithButton("Cool button", "button");
        
        await ReplyAsync(new FormattedMessage("POG???").WithComponents(builder));
    }

    [LinkComponentInteraction]
    public async Task ComponentInteraction(SocketMessageComponent component)
    {
        // Console.WriteLine("ComponentInteration linked method called " + component.Data.CustomId);
        

        try
        {

            await component.RespondAsync("Worked!");
        }
        catch (HttpException e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }


    }

    [LinkComponentInteraction("cool-id2")]
    public async Task IdComponentInteraction(SocketMessageComponent component)
    {
        // Console.WriteLine("ComponentInteration linked method called AT COOLD ID 2!!!!");
    } 
}

