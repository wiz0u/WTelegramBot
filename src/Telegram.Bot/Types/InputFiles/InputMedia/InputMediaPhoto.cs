using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// Represents a photo to be sent
/// </summary>
public partial class InputMediaPhoto :
    InputMedia,
    IAlbumInputMedia
{
    /// <inheritdoc />
    public override InputMediaType Type => InputMediaType.Photo;

    /// <summary>
    /// Optional. Pass <see langword="true"/> if the photo needs to be covered with a spoiler animation
    /// </summary>
    public bool? HasSpoiler { get; set; }

    /// <summary>
    /// Initializes a new photo media to send with an <see cref="InputFile"/>
    /// </summary>
    /// <param name="media">File to send</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InputMediaPhoto(InputFile media)
        : base(media)
    { }

    /// <summary>
    /// Initializes a new photo media to send with an <see cref="InputFile"/>
    /// </summary>
    public InputMediaPhoto()
    { }
}
