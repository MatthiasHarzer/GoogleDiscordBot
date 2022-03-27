﻿using System;

namespace GoogleBot.Interactions.Preconditions.Exceptions;

/// <summary>
/// The precondition wasn't satisfied 
/// </summary>
public class PreconditionFailedException : Exception
{
    public PreconditionFailedException()
    {
        
    }

    public PreconditionFailedException(string message) : base(message)
    {

    }
}