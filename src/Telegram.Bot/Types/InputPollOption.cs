namespace Telegram.Bot.Types;

/// <summary>
/// This object contains information about one answer option in a poll to send.
/// </summary>
public partial class InputPollOption
{
    /// <summary>
    /// Option text, 1-100 characters
    /// </summary>
    public string Text { get; set; } = default!;

    /// <summary>
    /// <em>Optional</em>. Mode for parsing entities in the text. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details. Currently, only custom emoji entities are allowed
    /// </summary>
    public Enums.ParseMode TextParseMode { get; set; }

    /// <summary>
    /// <em>Optional</em>. A list of special entities that appear in the poll option text. It can be specified instead of <see cref="TextParseMode">TextParseMode</see>
    /// </summary>
    public MessageEntity[]? TextEntities { get; set; }
}
