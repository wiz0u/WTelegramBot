using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to an MP3 audio file. By default, this audio file will be sent by the user.
/// Alternatively, you can use <see cref="InlineQueryResultAudio.InputMessageContent"/> to send
/// a message with the specified content instead of the audio.
/// </summary>
public partial class InlineQueryResultAudio : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be audio
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Audio;

    /// <summary>
    /// A valid URL for the audio file
    /// </summary>
    public required string AudioUrl { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public required string Title { get; set; }

    /// <inheritdoc cref="Documentation.Caption" />
    public string? Caption { get; set; }

    /// <inheritdoc cref="Documentation.ParseMode" />
    public ParseMode ParseMode { get; set; }

    /// <inheritdoc cref="Documentation.CaptionEntities" />
    public MessageEntity[]? CaptionEntities { get; set; }

    /// <summary>
    /// Optional. Performer
    /// </summary>
    public string? Performer { get; set; }

    /// <summary>
    /// Optional. Audio duration in seconds
    /// </summary>
    public int? AudioDuration { get; set; }

    /// <inheritdoc cref="Documentation.InputMessageContent" />
    public InputMessageContent? InputMessageContent { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="audioUrl">A valid URL for the audio file</param>
    /// <param name="title">Title of the result</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultAudio(string id, string audioUrl, string title)
        : base(id)
    {
        AudioUrl = audioUrl;
        Title = title;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultAudio()
    { }
}
