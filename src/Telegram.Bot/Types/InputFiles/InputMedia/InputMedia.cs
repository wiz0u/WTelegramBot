using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// This object represents the content of a media message to be sent
/// </summary>
public abstract partial class InputMedia
{
    /// <summary>
    /// Type of the media
    /// </summary>
    public abstract InputMediaType Type { get; }

    /// <summary>
    /// File to send. Pass a file_id to send a file that exists on the Telegram servers (recommended),
    /// pass an HTTP URL for Telegram to get a file from the Internet, or pass "attach://&lt;file_attach_name&gt;"
    /// to upload a new one using multipart/form-data under &lt;file_attach_name%gt; name.
    /// </summary>
    public required InputFile Media { get; set; }

    /// <summary>
    /// Optional. Caption of the photo to be sent, 0-1024 characters
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Optional. List of special entities that appear in the caption, which can be specified instead
    /// of <see cref="ParseMode"/>
    /// </summary>
    public MessageEntity[]? CaptionEntities { get; set; }

    /// <summary>
    /// Change, if you want Telegram apps to show bold, italic, fixed-width text or inline URLs in a caption
    /// </summary>
    public ParseMode ParseMode { get; set; }

    /// <summary>
    /// Initialize an object
    /// </summary>
    /// <param name="media">File to send</param>
    [SetsRequiredMembers]
    protected InputMedia(InputFile media) => Media = media;

    /// <summary>
    /// Initialize an object
    /// </summary>
    protected InputMedia()
    { }
}
