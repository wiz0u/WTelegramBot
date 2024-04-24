using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to a file. By default, this file will be sent by the user with an optional caption.
/// Alternatively, you can use <see cref="InlineQueryResultDocument.InputMessageContent"/> to send
/// a message with the specified content instead of the file. Currently, only .PDF and .ZIP files
/// can be sent using this method.
/// </summary>
public partial class InlineQueryResultDocument : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be document
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Document;

    /// <summary>
    /// Title for the result
    /// </summary>
    public required string Title { get; set; }

    /// <inheritdoc cref="Documentation.Caption" />
    public string? Caption { get; set; }

    /// <inheritdoc cref="Documentation.ParseMode" />
    public ParseMode ParseMode { get; set; }

    /// <inheritdoc cref="Documentation.CaptionEntities" />
    public MessageEntity[]? CaptionEntities { get; set; }

    /// <summary>
    /// A valid URL for the file
    /// </summary>
    public required string DocumentUrl { get; set; }

    /// <summary>
    /// Mime type of the content of the file, either “application/pdf” or “application/zip”
    /// </summary>
    public required string MimeType { get; set; }

    /// <summary>
    /// Optional. Short description of the result
    /// </summary>
    public string? Description { get; set; }

    /// <inheritdoc cref="Documentation.InputMessageContent" />
    public InputMessageContent? InputMessageContent { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailUrl" />
    public string? ThumbnailUrl { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailWidth" />
    public int? ThumbnailWidth { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailHeight" />
    public int? ThumbnailHeight { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="documentUrl">A valid URL for the file</param>
    /// <param name="title">Title of the result</param>
    /// <param name="mimeType">
    /// Mime type of the content of the file, either “application/pdf” or “application/zip”
    /// </param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultDocument(string id, string documentUrl, string title, string mimeType)
        : base(id)
    {
        DocumentUrl = documentUrl;
        Title = title;
        MimeType = mimeType;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultDocument()
    { }
}
