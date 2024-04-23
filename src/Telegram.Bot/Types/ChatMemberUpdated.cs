﻿
namespace Telegram.Bot.Types;

/// <summary>
/// This object represents changes in the status of a chat member.
/// </summary>
public partial class ChatMemberUpdated
{
    /// <summary>
    /// Chat the user belongs to
    /// </summary>
    public Chat Chat { get; set; } = default!;

    /// <summary>
    /// Performer of the action, which resulted in the change
    /// </summary>
    public User From { get; set; } = default!;

    /// <summary>
    /// Date the change was done
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Previous information about the chat member
    /// </summary>
    public ChatMember OldChatMember { get; set; } = default!;

    /// <summary>
    /// New information about the chat member
    /// </summary>
    public ChatMember NewChatMember { get; set; } = default!;

    /// <summary>
    /// Optional. Chat invite link, which was used by the user to join the chat; for joining by invite link
    /// events only.
    /// </summary>
    public ChatInviteLink? InviteLink { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the user joined the chat via a chat folder invite link
    /// </summary>
    public bool ViaChatFolderInviteLink { get; set; }
}
