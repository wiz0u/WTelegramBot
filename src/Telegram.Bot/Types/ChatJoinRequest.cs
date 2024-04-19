
namespace Telegram.Bot.Types;

/// <summary>
/// Represents a join request sent to a chat.
/// </summary>
public partial class ChatJoinRequest
{
    /// <summary>
    /// Chat to which the request was sent
    /// </summary>
    public Chat Chat { get; set; } = default!;

    /// <summary>
    /// User that sent the join request
    /// </summary>
    public User From { get; set; } = default!;

    /// <summary>
    /// Identifier of a private chat with the user who sent the join request. This number may have more than 32
    /// significant bits and some programming languages may have difficulty/silent defects in interpreting it. But it
    /// has at most 52 significant bits, so a 64-bit integer or double-precision float type are safe for storing this
    /// identifier. The bot can use this identifier for 24 hours to send messages until the join request is processed,
    /// assuming no other administrator contacted the user.
    /// </summary>
    public long UserChatId { get; set; }

    /// <summary>
    /// Date the request was sent
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Optional. Bio of the user
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Optional. Chat invite link that was used by the user to send the join request
    /// </summary>
    public ChatInviteLink? InviteLink { get; set; }
}
