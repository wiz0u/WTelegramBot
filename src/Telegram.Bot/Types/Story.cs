namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a message about a forwarded story in the chat. Currently holds no information.
/// </summary>
public partial class Story
{

    /// <summary>
    /// Chat that posted the story
    /// </summary>
    public Chat Chat { get; set; } = default!;

    /// <summary>
    /// Unique identifier for the story in the chat
    /// </summary>
    public int Id { get; set; }
}
