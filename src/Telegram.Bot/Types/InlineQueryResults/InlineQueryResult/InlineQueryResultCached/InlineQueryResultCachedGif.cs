using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to an animated GIF file stored on the Telegram servers. By default, this
/// animated GIF file will be sent by the user with an optional caption. Alternatively, you can
/// use <see cref="InlineQueryResultCachedGif.InputMessageContent"/> to send a message with
/// specified content instead of the animation.
/// </summary>
public partial class InlineQueryResultCachedGif : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be GIF
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Gif;

    /// <summary>
    /// A valid file identifier for the GIF file
    /// </summary>
    public required string GifFileId { get; set; }

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
    /// <param name="gifFileId">A valid file identifier for the GIF file</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultCachedGif(string id, string gifFileId)
        : base(id)
    {
        GifFileId = gifFileId;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultCachedGif()
    { }
}
