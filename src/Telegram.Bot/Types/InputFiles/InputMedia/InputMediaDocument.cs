using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// Represents a general file to be sent
/// </summary>
public partial class InputMediaDocument :
    InputMedia,
    IInputMediaThumb,
    IAlbumInputMedia
{
    /// <inheritdoc />
    public override InputMediaType Type => InputMediaType.Document;

    /// <inheritdoc />
    public InputFile? Thumbnail { get; set; }

    /// <summary>
    /// Optional. Disables automatic server-side content type detection for files uploaded using
    /// multipart/form-data. Always true, if the document is sent as part of an album.
    /// </summary>
    public bool DisableContentTypeDetection { get; set; }

    /// <summary>
    /// Initializes a new document media to send with an <see cref="InputMedia"/>
    /// </summary>
    /// <param name="media">File to send</param>
    [SetsRequiredMembers]
    public InputMediaDocument(InputFile media)
        : base(media)
    { }

    /// <summary>
    /// Initializes a new document media to send with an <see cref="InputMedia"/>
    /// </summary>
    public InputMediaDocument()
    { }
}
