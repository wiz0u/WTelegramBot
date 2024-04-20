using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// This object describes a sticker to be added to a sticker set.
/// <a href="https://core.telegram.org/bots/api#inputsticker"/>
/// </summary>
public partial class InputSticker
{
    /// <summary>
    /// The added sticker. Pass a <see cref="InputFileId"/> as a String to send a file that already exists
    /// on the Telegram servers, pass an HTTP URL as a String for Telegram to get a file
    /// from the Internet, or upload a new one using multipart/form-data.
    /// <see cref="StickerFormat.Animated">Animated</see> and <see cref="StickerFormat.Video">video</see>
    /// stickers can't be uploaded via HTTP URL.
    /// If you are using a <see cref="InputFileStream"/>, then the property <see cref="InputFileStream.FileName"/> is required.
    /// </summary>
    public required InputFile Sticker { get; set; }

    /// <summary>
    /// Format of the added sticker, must be one of “static” for a .WEBP or .PNG image, “animated” for a .TGS animation, “video” for a WEBM video
    /// </summary>
    public required StickerFormat Format { get; set; }

    /// <summary>
    /// List of 1-20 emoji associated with the sticker
    /// </summary>
    public required IEnumerable<string> EmojiList { get; set; }

    /// <summary>
    /// Optional. Position where the mask should be placed on faces.
    /// For <see cref="StickerType.Mask"/> stickers only.
    /// </summary>
    public MaskPosition? MaskPosition { get; set; }

    /// <summary>
    /// Optional. List of 0-20 search keywords for the sticker with total length of up to 64 characters.
    /// For <see cref="StickerType.Regular"/> and <see cref="StickerType.CustomEmoji"/> stickers only.
    /// </summary>
    public IEnumerable<string>? KeyWords { get; set; }

    /// <summary>
    /// Initializes a new input sticker to create or add sticker sets
    /// with an <see cref="InputFile">sticker</see> and emojiList
    /// </summary>
    /// <param name="sticker">
    /// The added sticker. Pass a file_id as a String to send a file that already exists
    /// on the Telegram servers, pass an HTTP URL as a String for Telegram to get a file
    /// from the Internet, or upload a new one using multipart/form-data.
    /// <see cref="StickerFormat.Animated">Animated</see> and <see cref="StickerFormat.Video">video</see>
    /// stickers can't be uploaded via HTTP URL.
    /// </param>
    /// <param name="emojiList">
    /// List of 1-20 emoji associated with the sticker
    /// </param>
    /// <param name="format">Format of the added sticker</param>
    [SetsRequiredMembers]
    public InputSticker(InputFile sticker, IEnumerable<string> emojiList, StickerFormat format)
    {
        Format = format;
        Sticker = sticker;
        EmojiList = emojiList;
    }

    /// <summary>
    /// Initializes a new input sticker to create or add sticker sets
    /// </summary>
    public InputSticker()
    { }
}
