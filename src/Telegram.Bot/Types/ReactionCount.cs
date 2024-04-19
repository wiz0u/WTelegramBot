namespace Telegram.Bot.Types;

/// <summary>
/// Represents a reaction added to a message along with the number of times it was added.
/// </summary>
public partial class ReactionCount
{
    /// <summary>
    /// Type of the reaction
    /// </summary>
    public ReactionType Type { get; set; } = default!;

    /// <summary>
    /// Number of times the reaction was added
    /// </summary>
    public int TotalCount { get; set; }
}
