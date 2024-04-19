namespace Telegram.Bot.Types.Passport;

/// <summary>
/// Contains information about Telegram Passport data shared with the bot by the user.
/// </summary>
public partial class PassportData
{
    /// <summary>
    /// Array with information about documents and other Telegram Passport elements that was shared with the bot.
    /// </summary>
    public EncryptedPassportElement[] Data { get; set; } = default!;

    /// <summary>
    /// Encrypted credentials required to decrypt the data.
    /// </summary>
    public EncryptedCredentials Credentials { get; set; } = default!;
}
