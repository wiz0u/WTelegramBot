using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents the content of a text message to be sent as the result of an
/// <see cref="InlineQuery">inline query</see>.
/// </summary>
public partial class InputTextMessageContent : InputMessageContent
{
    /// <summary>
    /// Text of the message to be sent, 1-4096 characters
    /// </summary>
    public required string MessageText { get; set; }

    /// <summary>
    /// Optional. Mode for
    /// <a href="https://core.telegram.org/bots/api#formatting-options">parsing entities</a> in the message
    /// text. See formatting options for more details.
    /// </summary>
    public ParseMode ParseMode { get; set; }

    /// <summary>
    /// Optional. List of special entities that appear in message text, which can be specified
    /// instead of <see cref="ParseMode"/>
    /// </summary>
    public MessageEntity[]? Entities { get; set; }

    /// <summary>
    /// Optional. Link preview generation options for the message
    /// </summary>
    public LinkPreviewOptions? LinkPreviewOptions { get; set; }

    /// <summary>
    /// Disables link previews for links in this message
    /// </summary>
    [Obsolete($"This property is deprecated, use {nameof(LinkPreviewOptions)} instead")]
    public bool DisableWebPagePreview
    {
        get => LinkPreviewOptions?.IsDisabled ?? false;
        set
        {
            LinkPreviewOptions ??= new();
            LinkPreviewOptions.IsDisabled = value;
        }
    }

    /// <summary>
    /// Initializes a new input text message content
    /// </summary>
    /// <param name="messageText">The text of the message</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InputTextMessageContent(string messageText)
    {
        MessageText = messageText;
    }

    /// <summary>
    /// Initializes a new input text message content
    /// </summary>
    public InputTextMessageContent()
    { }
}
