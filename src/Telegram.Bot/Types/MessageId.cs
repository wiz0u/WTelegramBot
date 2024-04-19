namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a messageId.
/// </summary>
public partial class MessageId
{
    /// <summary>
    /// Message identifier in the chat specified in <see cref="Requests.CopyMessageRequest.FromChatId"/>
    /// </summary>
    public int Id { get; set; }
}
