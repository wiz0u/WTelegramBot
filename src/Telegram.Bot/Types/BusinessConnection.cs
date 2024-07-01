namespace Telegram.Bot.Types;

/// <summary>Describes the connection of the bot with a business account.</summary>
public partial class BusinessConnection
{
    /// <summary>Unique identifier of the business connection</summary>
    public string Id { get; set; } = default!;

    /// <summary>Business account user that created the business connection</summary>
    public User User { get; set; } = default!;

    /// <summary>Identifier of a private chat with the user who created the business connection.</summary>
    public long UserChatId { get; set; }

    /// <summary>Date the connection was established</summary>
    public DateTime Date { get; set; }

    /// <summary><see langword="true"/>, if the bot can act on behalf of the business account in chats that were active in the last 24 hours</summary>
    public bool CanReply { get; set; }

    /// <summary><see langword="true"/>, if the connection is active</summary>
    public bool IsEnabled { get; set; }
}
