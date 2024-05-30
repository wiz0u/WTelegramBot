namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a boost removed from a chat.
/// </summary>
public partial class ChatBoostRemoved
{
    /// <summary>
    /// Chat which was boosted
    /// </summary>
    public Chat Chat { get; set; } = default!;

    /// <summary>
    /// Unique identifier of the boost
    /// </summary>
    public string BoostId { get; set; } = default!;

    /// <summary>
    /// Point in time when the boost was removed
    /// </summary>
    public DateTime RemoveDate { get; set; }

    /// <summary>
    /// Source of the removed boost
    /// </summary>
    public ChatBoostSource Source { get; set; } = default!;
}
