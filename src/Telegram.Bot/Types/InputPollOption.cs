namespace Telegram.Bot.Types;

/// <summary>
/// This object contains information about one answer option in a poll to send
/// </summary>
public partial class InputPollOption
{
    /// <summary>
    /// Option text, 1-100 characters
    /// </summary>
    public string Text { get; set; } = default!;

	/// <summary>
	/// Optional. Mode for parsing entities in the text. See formatting options for more details.
	/// Currently, only custom emoji entities are allowed
	/// </summary>
	public Enums.ParseMode TextParseMode { get; set; }

	/// <summary>
	/// Optional. Special entities that appear in the poll option text.
	/// Currently, only custom emoji entities are allowed in poll option texts
	/// </summary>
	public MessageEntity[]? TextEntities { get; set; }
}
