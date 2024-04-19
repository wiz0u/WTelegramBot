namespace Telegram.Bot.Types;

/// <summary>
/// This object contains information about one answer option in a poll.
/// </summary>
public partial class PollOption
{
    /// <summary>
    /// Option text, 1-100 characters
    /// </summary>
    public string Text { get; set; } = default!;

    /// <summary>
    /// Number of users that voted for this option
    /// </summary>
    public int VoterCount { get; set; }
}
