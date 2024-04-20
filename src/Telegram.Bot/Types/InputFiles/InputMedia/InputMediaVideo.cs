using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// Represents a video to be sent
/// </summary>
public partial class InputMediaVideo :
    InputMedia,
    IInputMediaThumb,
    IAlbumInputMedia
{
    /// <inheritdoc />
    public override InputMediaType Type => InputMediaType.Video;

    /// <inheritdoc />
    public InputFile? Thumbnail { get; set; }

    /// <summary>
    /// Optional. Video width
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Optional. Video height
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Optional. Video duration
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// Optional. Pass True, if the uploaded video is suitable for streaming
    /// </summary>
    public bool? SupportsStreaming { get; set; }

    /// <summary>
    /// Optional. Pass <see langword="true"/> if the video needs to be covered with a spoiler animation
    /// </summary>
    public bool? HasSpoiler { get; set; }

    /// <summary>
    /// Initializes a new video media to send with an <see cref="InputFile"/>
    /// </summary>
    /// <param name="media">File to send</param>
    [SetsRequiredMembers]
    public InputMediaVideo(InputFile media)
        : base(media)
    { }

    /// <summary>
    /// Initializes a new video media to send with an <see cref="InputFile"/>
    /// </summary>
    public InputMediaVideo()
    { }
}
