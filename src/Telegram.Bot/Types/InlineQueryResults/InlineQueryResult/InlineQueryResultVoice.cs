using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to a voice recording in an .OGG container encoded with OPUS. By default, this
/// voice recording will be sent by the user. Alternatively, you can use
/// <see cref="InlineQueryResultVoice.InputMessageContent"/> to send a message with the specified
/// content instead of the voice message.
/// </summary>
public partial class InlineQueryResultVoice : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be voice
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Voice;

    /// <summary>
    /// A valid URL for the voice recording
    /// </summary>
    public required string VoiceUrl { get; set; }

    /// <summary>
    /// Recording title
    /// </summary>
    public required string Title { get; set; }

    /// <inheritdoc cref="Documentation.Caption" />
    public string? Caption { get; set; }

    /// <inheritdoc cref="Documentation.ParseMode" />
    public ParseMode ParseMode { get; set; }

    /// <inheritdoc cref="Documentation.CaptionEntities" />
    public MessageEntity[]? CaptionEntities { get; set; }

    /// <summary>
    /// Optional. Recording duration in seconds
    /// </summary>
    public int? VoiceDuration { get; set; }

    /// <inheritdoc cref="Documentation.InputMessageContent" />
    public InputMessageContent? InputMessageContent { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="voiceUrl">A valid URL for the voice recording</param>
    /// <param name="title">Title of the result</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultVoice(string id, string voiceUrl, string title)
        : base(id)
    {
        VoiceUrl = voiceUrl;
        Title = title;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultVoice()
    { }
}
