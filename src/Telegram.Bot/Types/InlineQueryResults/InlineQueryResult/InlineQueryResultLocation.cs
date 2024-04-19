using System.Diagnostics.CodeAnalysis;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a location on a map. By default, the location will be sent by the user. Alternatively,
/// you can use <see cref="InlineQueryResultLocation.InputMessageContent"/> to send a message with
/// the specified content instead of the location.
/// </summary>
public partial class InlineQueryResultLocation : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be location
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Location;

    /// <inheritdoc cref="Documentation.Latitude" />
    public required double Latitude { get; set; }

    /// <inheritdoc cref="Documentation.Longitude" />
    public required double Longitude { get; set; }

    /// <summary>
    /// Location title
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Optional. The radius of uncertainty for the location, measured in meters; 0-1500
    /// </summary>
    public float? HorizontalAccuracy { get; set; }

    /// <summary>
    /// Optional. Period in seconds for which the location can be updated, should be between 60 and 86400.
    /// </summary>
    public int? LivePeriod { get; set; }

    /// <summary>
    /// Optional. For live locations, a direction in which the user is moving, in degrees.
    /// Must be between 1 and 360 if specified.
    /// </summary>
    public int? Heading { get; set; }

    /// <summary>
    /// Optional. For live locations, a maximum distance for proximity alerts about approaching
    /// another chat member, in meters. Must be between 1 and 100000 if specified.
    /// </summary>
    public int? ProximityAlertRadius { get; set; }

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
    /// <param name="latitude">Latitude of the location in degrees</param>
    /// <param name="longitude">Longitude of the location in degrees</param>
    /// <param name="title">Title of the result</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultLocation(string id, double latitude, double longitude, string title)
        : base(id)
    {
        Latitude = latitude;
        Longitude = longitude;
        Title = title;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultLocation()
    { }
}
