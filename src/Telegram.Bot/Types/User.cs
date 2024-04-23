namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a Telegram user or bot.
/// </summary>
public partial class User
{
    /// <summary>
    /// Unique identifier for this user or bot
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// <see langword="true"/>, if this user is a bot
    /// </summary>
    public bool IsBot { get; set; }

    /// <summary>
    /// User's or bot’s first name
    /// </summary>
    public string FirstName { get; set; } = default!;

    /// <summary>
    /// Optional. User's or bot’s last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Optional. User's or bot’s username
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Optional. <a href="https://en.wikipedia.org/wiki/IETF_language_tag">IETF language tag</a> of the
    /// user's language
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if this user is a Telegram Premium user
    /// </summary>
    public bool IsPremium { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if this user added the bot to the attachment menu
    /// </summary>
    public bool AddedToAttachmentMenu { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the bot can be invited to groups.
    /// Returned only in <see cref="Requests.GetMeRequest"/>
    /// </summary>
    public bool CanJoinGroups { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if privacy mode is disabled for the bot.
    /// Returned only in <see cref="Requests.GetMeRequest"/>
    /// </summary>
    public bool CanReadAllGroupMessages { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the bot supports inline queries.
    /// Returned only in <see cref="Requests.GetMeRequest"/>
    /// </summary>
    public bool SupportsInlineQueries { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the bot can be connected to a Telegram Business account to receive its
    /// messages. Returned only in <see cref="Requests.GetMeRequest"/>
    /// </summary>
    public bool CanConnectToBusiness { get; set; }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{(Username is null ? $"{FirstName}{LastName?.Insert(0, " ")}" : $"@{Username}")} ({Id})";
}
