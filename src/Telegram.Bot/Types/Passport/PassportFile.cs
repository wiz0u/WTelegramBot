namespace Telegram.Bot.Types.Passport;

/// <summary>
/// This object represents a file uploaded to Telegram Passport. Currently all Telegram Passport files are in JPEG format when decrypted and don't exceed 10MB.
/// </summary>
public partial class PassportFile : FileBase
{
    /// <summary>
    /// DateTime when the file was uploaded
    /// </summary>
    public DateTime FileDate { get; set; }
}
