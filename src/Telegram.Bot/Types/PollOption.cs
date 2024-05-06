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
	/// Optional. Special entities that appear in the option text.
	/// Currently, only custom emoji entities are allowed in poll option texts
	/// </summary>
	public MessageEntity[]? TextEntities { get; set; }
	
    /// <summary>
    /// Number of users that voted for this option
    /// </summary>
    public int VoterCount { get; set; }
}
