using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to a voice message stored on the Telegram servers. By default, this voice
/// message will be sent by the user. Alternatively, you can use
/// <see cref="InlineQueryResultCachedVoice.InputMessageContent"/> to send a message
/// with the specified content instead of the voice message.
/// </summary>
public partial class InlineQueryResultCachedVoice : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be voice
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Voice;

    /// <summary>
    /// A valid file identifier for the voice message
    /// </summary>
    public required string VoiceFileId { get; set; }

    /// <summary>
    /// Voice message title
    /// </summary>
    public required string Title { get; set; }

    /// <inheritdoc cref="Documentation.Caption" />
    public string? Caption { get; set; }

    /// <inheritdoc cref="Documentation.ParseMode" />
    public ParseMode ParseMode { get; set; }

    /// <inheritdoc cref="Documentation.CaptionEntities" />
    public MessageEntity[]? CaptionEntities { get; set; }

    /// <inheritdoc cref="Documentation.InputMessageContent" />
    public InputMessageContent? InputMessageContent { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="fileId">A valid file identifier for the voice message</param>
    /// <param name="title">Title of the result</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultCachedVoice(string id, string fileId, string title)
        : base(id)
    {
        VoiceFileId = fileId;
        Title = title;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultCachedVoice()
    { }
}
