using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to a file stored on the Telegram servers. By default, this file will be sent
/// by the user with an optional caption. Alternatively, you can use
/// <see cref="InlineQueryResultCachedDocument.InputMessageContent"/> to send a message with the
/// specified content instead of the file.
/// </summary>
public partial class InlineQueryResultCachedDocument : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be document
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Document;

    /// <summary>
    /// Title for the result
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// A valid file identifier for the file
    /// </summary>
    public required string DocumentFileId { get; set; }

    /// <summary>
    /// Optional. Short description of the result
    /// </summary>
    public string? Description { get; set; }

    /// <inheritdoc cref="Documentation.Caption" />
    public string? Caption { get; set; }

    /// <inheritdoc cref="Documentation.ParseMode" />
    public ParseMode? ParseMode { get; set; }

    /// <inheritdoc cref="Documentation.CaptionEntities" />
    public MessageEntity[]? CaptionEntities { get; set; }

    /// <inheritdoc cref="Documentation.InputMessageContent" />
    public InputMessageContent? InputMessageContent { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="documentFileId">A valid file identifier for the file</param>
    /// <param name="title">Title of the result</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultCachedDocument(string id, string documentFileId, string title)
        : base(id)
    {
        DocumentFileId = documentFileId;
        Title = title;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultCachedDocument()
    { }
}
