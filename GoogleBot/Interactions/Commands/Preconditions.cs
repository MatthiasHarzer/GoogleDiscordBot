namespace GoogleBot.Interactions.Commands;

/// <summary>
/// Preconditions for a command
/// </summary>
public class Preconditions
{
    public bool RequiresMajority { get; init; }

    public string MajorityVoteButtonText { get; init; } = string.Empty;

    public bool RequiresBotConnected { get; init; }
}