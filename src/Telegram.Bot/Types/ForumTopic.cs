namespace Telegram.Bot.Types;

/// <summary>This object represents a forum topic.</summary>
public partial class ForumTopic
{
    /// <summary>Unique identifier of the forum topic</summary>
    public int MessageThreadId { get; set; }

    /// <summary>Name of the topic</summary>
    public string Name { get; set; } = default!;

    /// <summary>Color of the topic icon in RGB format</summary>
    public int IconColor { get; set; }

    /// <summary><em>Optional</em>. Unique identifier of the custom emoji shown as the topic icon</summary>
    public string? IconCustomEmojiId { get; set; }
}
