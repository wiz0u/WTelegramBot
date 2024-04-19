namespace Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Upon receiving a <see cref="Message"/> with this object, Telegram clients will display a reply interface to the
/// user (act as if the user has selected the bot’s message and tapped 'Reply'). This can be extremely useful if you
/// want to create user-friendly step-by-step interfaces without having to sacrifice
/// <a href="https://core.telegram.org/bots#privacy-mode">privacy mode</a>.
/// </summary>
public partial class ForceReplyMarkup : ReplyMarkupBase
{
    /// <summary>
    /// Shows reply interface to the user, as if they manually selected the bot’s message and tapped 'Reply'
    /// </summary>
    public bool ForceReply => true;

    /// <summary>
    /// Optional. The placeholder to be shown in the input field when the reply is active; 1-64 characters
    /// </summary>
    public string? InputFieldPlaceholder { get; set; }
}
