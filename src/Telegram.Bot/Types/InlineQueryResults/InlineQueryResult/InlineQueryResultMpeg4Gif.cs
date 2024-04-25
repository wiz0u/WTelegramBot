﻿using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to a video animation (H.264/MPEG-4 AVC video without sound). By default, this
/// animated MPEG-4 file will be sent by the user with optional caption. Alternatively, you can use
/// <see cref="InlineQueryResultMpeg4Gif.InputMessageContent"/> to send a message with the specified
/// content instead of the animation.
/// </summary>
public partial class InlineQueryResultMpeg4Gif : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be mpeg4_gif
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Mpeg4Gif;

    /// <summary>
    /// A valid URL for the MP4 file. File size must not exceed 1MB
    /// </summary>
    public required string Mpeg4Url { get; set; }

    /// <summary>
    /// Optional. Video width
    /// </summary>
    public int? Mpeg4Width { get; set; }

    /// <summary>
    /// Optional. Video height
    /// </summary>
    public int? Mpeg4Height { get; set; }

    /// <summary>
    /// Optional. Video duration
    /// </summary>
    public int? Mpeg4Duration { get; set; }

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
    public ParseMode ParseMode { get; set; }

    /// <inheritdoc cref="Documentation.CaptionEntities" />
    public MessageEntity[]? CaptionEntities { get; set; }

    /// <inheritdoc cref="Documentation.InputMessageContent" />
    public InputMessageContent? InputMessageContent { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="mpeg4Url">A valid URL for the MP4 file. File size must not exceed 1MB.</param>
    /// <param name="thumbnailUrl">Url of the thumbnail for the result.</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultMpeg4Gif(string id, string mpeg4Url, string thumbnailUrl)
        : base(id)
    {
        Mpeg4Url = mpeg4Url;
        ThumbnailUrl = thumbnailUrl;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultMpeg4Gif()
    { }
}