namespace Telegram.Bot.Types.Payments;

/// <summary>This object contains information about an incoming shipping query.</summary>
public partial class ShippingQuery
{
    /// <summary>Unique query identifier</summary>
    public string Id { get; set; } = default!;

    /// <summary>User who sent the query</summary>
    public User From { get; set; } = default!;

    /// <summary>Bot specified invoice payload</summary>
    public string InvoicePayload { get; set; } = default!;

    /// <summary>User specified shipping address</summary>
    public ShippingAddress ShippingAddress { get; set; } = default!;
}
