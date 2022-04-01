using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GoogleBot.Interactions.Context;
using GoogleBot.Interactions.Modules;

namespace GoogleBot.Interactions.Preconditions;

/// <summary>
/// The base attribute for all preconditions
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public abstract class PreconditionAttribute : Attribute
{
    /// <summary>
    /// The command context
    /// </summary>
    public ICommandContext Context { get; private set; } = null!;

    /// <summary>
    /// Sets the context of the precondition
    /// </summary>
    /// <param name="context">The context</param>
    /// <returns>It self</returns>
    public PreconditionAttribute WithContext(ICommandContext context)
    {
        Context = context;
        return this;
    }

    /// <summary>
    /// Checks if the precondition is met and if not, trys to satisfies it.
    /// </summary>
    /// <exception cref="PreconditionNotSatisfiedException">The precondition can't be satisfied.</exception>
    /// <returns></returns>
    public abstract Task Satisfy();
    
}
