namespace Telegram.Bot.Types;

/// <summary>Describes the opening hours of a business.</summary>
public partial class BusinessOpeningHours
{
    /// <summary>Unique name of the time zone for which the opening hours are defined</summary>
    public string TimeZoneName { get; set; } = default!;

    /// <summary>List of time intervals describing business opening hours</summary>
    public BusinessOpeningHoursInterval[] OpeningHours { get; set; } = default!;
}
