using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// This object describes the type of a reaction. Currently, it can be one of
/// <list type="bullet">
/// <item><see cref="ReactionTypeEmoji"/></item>
/// <item><see cref="ReactionTypeCustomEmoji"/></item>
/// </list>
/// </summary>
public abstract partial class ReactionType
{
    /// <summary>
    /// Type of the reaction
    /// </summary>
    public abstract ReactionTypeKind Type { get; }
}

/// <summary>
/// The reaction is based on an emoji.
/// </summary>
public partial class ReactionTypeEmoji : ReactionType
{
    /// <summary>
    /// Type of the reaction, always "emoji"
    /// </summary>
    public override ReactionTypeKind Type => ReactionTypeKind.Emoji;

    /// <summary>
    /// Reaction emoji. Currently, it can be one of "👍", "👎", "❤", "🔥", "🥰", "👏", "😁",
    /// "🤔", "🤯", "😱", "🤬", "😢", "🎉", "🤩", "🤮", "💩", "🙏", "👌", "🕊", "🤡", "🥱",
    /// "🥴", "😍", "🐳", "❤‍🔥", "🌚", "🌭", "💯", "🤣", "⚡", "🍌", "🏆", "💔", "🤨",
    /// "😐", "🍓", "🍾", "💋", "🖕", "😈", "😴", "😭", "🤓", "👻", "👨‍💻", "👀", "🎃",
    /// "🙈", "😇", "😨", "🤝", "✍", "🤗", "🫡", "🎅", "🎄", "☃", "💅", "🤪", "🗿", "🆒",
    /// "💘", "🙉", "🦄", "😘", "💊", "🙊", "😎", "👾", "🤷‍♂", "🤷", "🤷‍♀", "😡"
    /// </summary>
    /// <remarks>
    /// Available shortcuts: <see cref="Enums.KnownReactionTypeEmoji"/>
    /// </remarks>
    public string Emoji { get; set; } = default!;
}

/// <summary>
/// The reaction is based on an emoji.
/// </summary>
public partial class ReactionTypeCustomEmoji : ReactionType
{
    /// <summary>
    /// Type of the reaction, always "custom_emoji"
    /// </summary>
    public override ReactionTypeKind Type => ReactionTypeKind.CustomEmoji;

    /// <summary>
    /// Custom emoji identifier
    /// </summary>
    public string CustomEmojiId { get; set; } = default!;
}
