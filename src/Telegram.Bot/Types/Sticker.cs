using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a sticker.
/// <a href="https://core.telegram.org/bots/api#sticker"/>
/// </summary>
public partial class Sticker : FileBase
{
    /// <summary>
    /// Type of the sticker. The type of the sticker is independent from its format,
    /// which is determined by the fields <see cref="IsAnimated"/> and <see cref="IsVideo"/>.
    /// </summary>
    public StickerType Type { get; set; }

    /// <summary>
    /// Sticker width
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Sticker height
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the sticker is <see cref="StickerFormat.Animated">animated</see>
    /// </summary>
    public bool IsAnimated { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the sticker is a <see cref="StickerFormat.Video">video sticker</see>
    /// </summary>
    public bool IsVideo { get; set; }

    /// <summary>
    /// Optional. Sticker thumbnail in the .WEBP or .JPG format
    /// </summary>
    public PhotoSize? Thumbnail { get; set; }

    /// <summary>
    /// Optional. Emoji associated with the sticker
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    /// Optional. Name of the sticker set to which the sticker belongs
    /// </summary>
    public string? SetName { get; set; }

    /// <summary>
    /// Optional. For premium <see cref="StickerType.Regular">regular</see> stickers,
    /// premium animation for the sticker
    /// </summary>
    public File? PremiumAnimation { get; set; }

    /// <summary>
    /// Optional. For <see cref="StickerType.Mask">mask</see> stickers,
    /// the position where the mask should be placed
    /// </summary>
    public MaskPosition? MaskPosition { get; set; }

    /// <summary>
    /// Optional. For <see cref="StickerType.CustomEmoji">custom emoji</see> stickers,
    /// unique identifier of the custom emoji
    /// </summary>
    public string? CustomEmojiId { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the sticker must be repainted to a text color
    /// in <see cref="Message">messages</see>, the color of the Telegram Premium badge in emoji
    /// status, white color on <see cref="ChatPhoto">chat photos</see>, or another appropriate
    /// color in other places
    /// </summary>
    public bool NeedsRepainting { get; set; }
}
