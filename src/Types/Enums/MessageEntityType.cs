namespace Telegram.Bot.Types.Enums;

/// <summary>
/// Type of a <see cref="MessageEntity"/>
/// </summary>
public static class MessageEntityType
{
    /// <summary>
    /// A mentioned <see cref="User"/>
    /// </summary>
    public const string Mention = "Mention";

    /// <summary>
    /// A searchable Hashtag
    /// </summary>
    public const string Hashtag = "Hashtag";

    /// <summary>
    /// A Bot command
    /// </summary>
    public const string BotCommand = "BotCommand";

    /// <summary>
    /// An URL
    /// </summary>
    public const string Url = "Url";

    /// <summary>
    /// An email
    /// </summary>
    public const string Email = "Email";

    /// <summary>
    /// Bold text
    /// </summary>
    public const string Bold = "Bold";

    /// <summary>
    /// Italic text
    /// </summary>
    public const string Italic = "Italic";

    /// <summary>
    /// Monowidth string
    /// </summary>
    public const string Code = "Code";

    /// <summary>
    /// Monowidth block
    /// </summary>
    public const string Pre = "Pre";

    /// <summary>
    /// Clickable text URLs
    /// </summary>
    public const string TextLink = "TextUrl";

    /// <summary>
    /// Mentions for a <see cref="User"/> without <see cref="User.Username"/>
    /// </summary>
    public const string TextMention = "MentionName";

    /// <summary>
    /// Phone number
    /// </summary>
    public const string PhoneNumber = "Phone";

    /// <summary>
    /// A cashtag (e.g. $EUR, $USD) - $ followed by the short currency code
    /// </summary>
	public const string Cashtag = "Cashtag";

    /// <summary>
    /// Underlined text
    /// </summary>
    public const string Underline = "Underline";

    /// <summary>
    /// Strikethrough text
    /// </summary>
    public const string Strikethrough = "Strike";

    /// <summary>
    /// Spoiler message
    /// </summary>
    public const string Spoiler = "Spoiler";

    /// <summary>
    /// Inline custom emoji stickers
    /// </summary>
    public const string CustomEmoji = "CustomEmoji";
}
