using System.Diagnostics.CodeAnalysis;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents the content of a location message to be sent as the result of an
/// <see cref="InlineQuery">inline query</see>.
/// </summary>
public partial class InputLocationMessageContent : InputMessageContent
{
    /// <summary>
    /// Latitude of the location in degrees
    /// </summary>
    public required double Latitude { get; set; }

    /// <summary>
    /// Longitude of the location in degrees
    /// </summary>
    public required double Longitude { get; set; }

    /// <summary>
    /// Optional. The radius of uncertainty for the location, measured in meters; 0-1500
    /// </summary>
    public float? HorizontalAccuracy { get; set; }

    /// <summary>
    /// Optional. Period in seconds for which the location can be updated, should be between 60 and 86400.
    /// </summary>
    public int? LivePeriod { get; set; }

    /// <summary>
    /// Optional. The direction in which user is moving, in degrees; 1-360. For active live locations only.
    /// </summary>
    public int? Heading { get; set; }

    /// <summary>
    /// Optional. Maximum distance for proximity alerts about approaching another chat member,
    /// in meters. For sent live locations only.
    /// </summary>
    public int? ProximityAlertRadius { get; set; }

    /// <summary>
    /// Initializes a new input location message content
    /// </summary>
    /// <param name="latitude">The latitude of the location</param>
    /// <param name="longitude">The longitude of the location</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InputLocationMessageContent(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Initializes a new input location message content
    /// </summary>
    public InputLocationMessageContent()
    { }
}
