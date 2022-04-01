using System;
using GoogleBot.Interactions.Commands;

namespace GoogleBot.Exceptions;

public class CommandParameterException : Exception
{
    public CommandParameterException(ParameterInfo parameter, string message) : base(message)
    {
        Console.WriteLine($"Parameter `{parameter}` invalid.");
    }
}