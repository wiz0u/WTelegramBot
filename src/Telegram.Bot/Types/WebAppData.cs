namespace Telegram.Bot.Types;

/// <summary>
/// Contains data sent from a <a href="https://core.telegram.org/bots/webapps"></a>Web App to the bot.
/// </summary>
public partial class WebAppData
{
    /// <summary>
    /// The data. Be aware that a bad client can send arbitrary data in this field.
    /// </summary>
    public string Data { get; set; } = default!;

    /// <summary>
    /// Text of the web_app keyboard button, from which the Web App was opened. Be aware that a bad client can
    /// send arbitrary data in this field.
    /// </summary>
    public string ButtonText { get; set; } = default!;
}
