using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to an animated GIF file. By default, this animated GIF file will be sent by the
/// user with optional caption. Alternatively, you can use
/// <see cref="InlineQueryResultGif.InputMessageContent"/> to send a message with the
/// specified content instead of the animation.
/// </summary>
public partial class InlineQueryResultGif : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be GIF
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Gif;

    /// <summary>
    /// A valid URL for the GIF file. File size must not exceed 1MB
    /// </summary>
    public required string GifUrl { get; set; }

    /// <summary>
    /// Optional. Width of the GIF.
    /// </summary>
    public int? GifWidth { get; set; }

    /// <summary>
    /// Optional. Height of the GIF.
    /// </summary>
    public int? GifHeight { get; set; }

    /// <summary>
    /// Optional. Duration of the GIF.
    /// </summary>
    public int? GifDuration { get; set; }

    /// <summary>
    /// URL of the static (JPEG or GIF) or animated (MPEG4) thumbnail for the result
    /// </summary>
    public required string ThumbnailUrl { get; set; }

    /// <summary>
    /// Optional. MIME type of the thumbnail, must be one of “image/jpeg”, “image/gif”,
    /// or “video/mp4”. Defaults to “image/jpeg”
    /// </summary>
    public string? ThumbnailMimeType { get; set; }

    /// <summary>
    /// Optional. Title for the result
    /// </summary>
    public string? Title { get; set; }

    /// <inheritdoc cref="Documentation.Caption" />
    public string? Caption { get; set; }

    /// <inheritdoc cref="Documentation.ParseMode" />
    public ParseMode? ParseMode { get; set; }

    /// <inheritdoc cref="Documentation.CaptionEntities" />
    public MessageEntity[]? CaptionEntities { get; set; }

    /// <inheritdoc cref="Documentation.InputMessageContent" />
    public InputMessageContent? InputMessageContent { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="gifUrl">Width of the GIF</param>
    /// <param name="thumbnailUrl">Url of the thumbnail for the result.</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultGif(string id, string gifUrl, string thumbnailUrl)
        : base(id)
    {
        GifUrl = gifUrl;
        ThumbnailUrl = thumbnailUrl;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultGif()
    { }
}
