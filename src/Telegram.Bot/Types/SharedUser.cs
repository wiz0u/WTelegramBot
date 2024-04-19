namespace Telegram.Bot.Types;

/// <summary>
/// This object contains information about a user that was shared with the bot using
/// a <see cref="ReplyMarkups.KeyboardButtonRequestUser"/> button.
/// </summary>
public partial class SharedUser
{
    /// <summary>
    /// Identifier of the shared user. The bot may not have access to the user and could be unable to use this
    /// identifier, unless the user is already known to the bot by some other means.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Optional. First name of the user, if the name was requested by the bot
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Optional. Last name of the user, if the name was requested by the bot
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Optional. Username of the user, if the username was requested by the bot
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///
    /// </summary>
    public PhotoSize[]? Photo { get; set; }
}
