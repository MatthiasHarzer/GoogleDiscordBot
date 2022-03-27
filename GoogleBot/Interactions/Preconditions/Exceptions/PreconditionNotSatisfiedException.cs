using System;

namespace GoogleBot.Interactions.Preconditions.Exceptions;

public class PreconditionNotSatisfiedException : Exception
{
    public FormattedMessage FormattedMessage { get; }
    
    public PreconditionNotSatisfiedException(FormattedMessage message)
    {
        FormattedMessage = message;
    }
}