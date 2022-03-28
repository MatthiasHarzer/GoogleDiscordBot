using System;

namespace GoogleBot.Interactions.Preconditions.Exceptions;

/// <summary>
/// The precondition wasn't satisfied 
/// </summary>
public class PreconditionFailedException : Exception
{
    public bool Responded { get; }
    public PreconditionFailedException()
    {
        
    }

    public PreconditionFailedException(string message, bool responded = false) : base(message)
    {
        Responded = responded;
    }
}