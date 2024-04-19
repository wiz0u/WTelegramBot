namespace Telegram.Bot.Types.Payments;

/// <summary>
/// This object represents one shipping option.
/// </summary>
public partial class ShippingOption
{
    /// <summary>
    /// Shipping option identifier
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// Option title
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// List of price portions
    /// </summary>
    public LabeledPrice[] Prices { get; set; } = default!;
}
