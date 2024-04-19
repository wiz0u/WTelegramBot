using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a sticker set.
/// <a href="https://core.telegram.org/bots/api#stickerset"/>
/// </summary>
public partial class StickerSet
{
    /// <summary>
    /// Sticker set name
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Sticker set title
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Type of stickers in the set
    /// </summary>
    public StickerType StickerType { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the sticker set contains <see cref="StickerFormat.Animated">animated stickers</see>
    /// </summary>
    [Obsolete("This field is no longer sent by Bot API")]
    public bool IsAnimated { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the sticker set contains <see cref="StickerFormat.Video">video stickers</see>
    /// </summary>
    [Obsolete("This field is no longer sent by Bot API")]
    public bool IsVideo { get; set; }

    /// <summary>
    /// List of all set stickers
    /// </summary>
    public Sticker[] Stickers { get; set; } = default!;

    /// <summary>
    /// Optional. Sticker set thumbnail in the .WEBP, .TGS, or .WEBM format
    /// </summary>
    public PhotoSize? Thumbnail { get; set; }
}
