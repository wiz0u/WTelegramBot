namespace Telegram.Bot.Types.Payments;

/// <summary>
/// This object contains basic information about an invoice.
/// </summary>
public partial class Invoice
{
    /// <summary>
    /// Product name
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Product description
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// Unique bot deep-linking parameter that can be used to generate this invoice
    /// </summary>
    public string StartParameter { get; set; } = default!;

    /// <summary>
    /// Three-letter ISO 4217 <a href="https://core.telegram.org/bots/payments#supported-currencies">currency</a> code
    /// </summary>
    public string Currency { get; set; } = default!;

    /// <summary>
    /// Total price in the <em>smallest units</em> of the currency (integer, <b>not</b> float/double). For example, for a price of <c>US$ 1.45</c> pass <c>amount = 145</c>. See the <em>exp</em> parameter in <a href="https://core.telegram.org/bots/payments/currencies.json">currencies.json</a>, it shows the number of digits past the decimal point for each currency (2 for the majority of currencies).
    /// </summary>
    public int TotalAmount { get; set; }
}
