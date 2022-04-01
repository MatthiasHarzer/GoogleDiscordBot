using System;

namespace GoogleBot.Exceptions;

public class PreconditionNotSatisfiedException : Exception
{
    public FormattedMessage FormattedMessage { get; }
    
    public PreconditionNotSatisfiedException(FormattedMessage message)
    {
        FormattedMessage = message;
    }
}