﻿namespace Telegram.Bot.Types;

/// <summary>This object describes the origin of a message. It can be one of<br/><see cref="MessageOriginUser"/>, <see cref="MessageOriginHiddenUser"/>, <see cref="MessageOriginChat"/>, <see cref="MessageOriginChannel"/></summary>
public abstract partial class MessageOrigin
{
    /// <summary>Type of the message origin</summary>
    public abstract MessageOriginType Type { get; }

    /// <summary>Date the message was sent originally</summary>
    public DateTime Date { get; set; }
}

/// <summary>The message was originally sent by a known user.</summary>
public partial class MessageOriginUser : MessageOrigin
{
    /// <summary>Type of the message origin, always <see cref="MessageOriginType.User"/></summary>
    public override MessageOriginType Type => MessageOriginType.User;

    /// <summary>User that sent the message originally</summary>
    public User SenderUser { get; set; } = default!;
}

/// <summary>The message was originally sent by an unknown user.</summary>
public partial class MessageOriginHiddenUser : MessageOrigin
{
    /// <summary>Type of the message origin, always <see cref="MessageOriginType.HiddenUser"/></summary>
    public override MessageOriginType Type => MessageOriginType.HiddenUser;

    /// <summary>Name of the user that sent the message originally</summary>
    public string SenderUserName { get; set; } = default!;
}

/// <summary>The message was originally sent on behalf of a chat to a group chat.</summary>
public partial class MessageOriginChat : MessageOrigin
{
    /// <summary>Type of the message origin, always <see cref="MessageOriginType.Chat"/></summary>
    public override MessageOriginType Type => MessageOriginType.Chat;

    /// <summary>Chat that sent the message originally</summary>
    public Chat SenderChat { get; set; } = default!;

    /// <summary><em>Optional</em>. For messages originally sent by an anonymous chat administrator, original message author signature</summary>
    public string? AuthorSignature { get; set; }
}

/// <summary>The message was originally sent to a channel chat.</summary>
public partial class MessageOriginChannel : MessageOrigin
{
    /// <summary>Type of the message origin, always <see cref="MessageOriginType.Channel"/></summary>
    public override MessageOriginType Type => MessageOriginType.Channel;

    /// <summary>Channel chat to which the message was originally sent</summary>
    public Chat Chat { get; set; } = default!;

    /// <summary>Unique message identifier inside the chat</summary>
    public int MessageId { get; set; }

    /// <summary><em>Optional</em>. Signature of the original post author</summary>
    public string? AuthorSignature { get; set; }
}
