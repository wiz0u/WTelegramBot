using System.Diagnostics.CodeAnalysis;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a link to an article or web page.
/// </summary>
public partial class InlineQueryResultArticle : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be article
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Article;

    /// <summary>
    /// Title of the result
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Content of the message to be sent
    /// </summary>
    public required InputMessageContent InputMessageContent { get; set; }

    /// <summary>
    /// Optional. URL of the result.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Optional. Pass <see langword="true"/>, if you don't want the URL to be shown in the message.
    /// </summary>
    public bool? HideUrl { get; set; }

    /// <summary>
    /// Optional. Short description of the result.
    /// </summary>
    public string? Description { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailUrl" />
    public string? ThumbnailUrl { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailWidth" />
    public int? ThumbnailWidth { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailHeight" />
    public int? ThumbnailHeight { get; set; }

    /// <summary>
    /// Initializes a new <see cref="InlineQueryResultArticle"/> object
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="title">Title of the result</param>
    /// <param name="inputMessageContent">Content of the message to be sent</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultArticle(string id, string title, InputMessageContent inputMessageContent)
        : base(id)
    {
        Title = title;
        InputMessageContent = inputMessageContent;
    }

    /// <summary>
    /// Initializes a new <see cref="InlineQueryResultArticle"/> object
    /// </summary>
    public InlineQueryResultArticle()
    { }
}
