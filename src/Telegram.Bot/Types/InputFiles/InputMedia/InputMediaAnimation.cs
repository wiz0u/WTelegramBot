using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// Represents an animation file (GIF or H.264/MPEG-4 AVC video without sound) to be sent.
/// </summary>
public partial class InputMediaAnimation :
    InputMedia,
    IInputMediaThumb
{
    /// <inheritdoc />
    public override InputMediaType Type => InputMediaType.Animation;

    /// <inheritdoc />
    public InputFile? Thumbnail { get; set; }

    /// <summary>
    /// Optional. Animation width
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Optional. Animation height
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Optional. Animation duration
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// Optional. Pass <see langword="true"/> if the animation needs to be covered with a spoiler animation
    /// </summary>
    public bool? HasSpoiler { get; set; }

    /// <summary>
    /// Initializes a new animation media to send with an <see cref="InputFile"/>
    /// </summary>
    /// <param name="media">File to send</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InputMediaAnimation(InputFile media)
        : base(media)
    { }

    /// <summary>
    /// Initializes a new animation media to send with an <see cref="InputFile"/>
    /// </summary>
    public InputMediaAnimation()
    { }
}
