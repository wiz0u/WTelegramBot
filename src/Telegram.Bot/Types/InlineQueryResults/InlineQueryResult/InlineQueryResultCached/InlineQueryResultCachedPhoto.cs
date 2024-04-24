using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to a photo stored on the Telegram servers. By default, this photo will be sent
/// by the user with an optional caption. Alternatively, you can use
/// <see cref="InlineQueryResultCachedPhoto.InputMessageContent"/> to send a message with the
/// specified content instead of the photo.
/// </summary>
public partial class InlineQueryResultCachedPhoto : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be photo
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Photo;

    /// <summary>
    /// A valid file identifier of the photo
    /// </summary>
    public required string PhotoFileId { get; set; }

    /// <summary>
    /// Optional. Title for the result
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Optional. Short description of the result
    /// </summary>
    public string? Description { get; set; }

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
    /// <param name="photoFileId">A valid file identifier of the photo</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultCachedPhoto(string id, string photoFileId)
        : base(id)
    {
        PhotoFileId = photoFileId;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultCachedPhoto()
    { }
}
