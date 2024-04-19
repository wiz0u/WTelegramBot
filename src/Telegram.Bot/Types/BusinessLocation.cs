namespace Telegram.Bot.Types;

/// <summary>
///
/// </summary>
public partial class BusinessLocation
{
    /// <summary>
    /// Address of the business
    /// </summary>
    public string Address { get; set; } = default!;

    /// <summary>
    /// Optional. Location of the business
    /// </summary>
    public Location? Location { get; set; }
}
