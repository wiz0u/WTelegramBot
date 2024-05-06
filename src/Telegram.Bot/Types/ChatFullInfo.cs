namespace Telegram.Bot.Types;

/// <summary>
/// This object contains full information about a chat.
/// </summary>
public partial class ChatFullInfo : Chat
{
	/// <summary>
	/// Optional. Identifier of the <see href="https://core.telegram.org/bots/api#accent-colors">accent color</see>
	/// for the chat name and backgrounds of the chat photo, reply header, and link preview.
	/// See accent colors for more details.
	/// </summary>
	public int AccentColorId { get; set; }

	/// <summary>
	/// The maximum number of reactions that can be set on a message in the chat
	/// </summary>
	public int MaxReactionCount { get; set; }

	/// <summary>
	/// Optional. Chat photo.
	/// </summary>
	public ChatPhoto? Photo { get; set; }

    /// <summary>
    /// Optional. If non-empty, the list of all active chat usernames; for private chats, supergroups and channels.
    /// </summary>
    public string[]? ActiveUsernames { get; set; }

    /// <summary>
    /// Optional. For private chats, the date of birth of the user.
    /// </summary>
    public Birthday? Birthday { get; set; }

    /// <summary>
    /// Optional. For private chats with business accounts, the intro of the business.
    /// </summary>
    public BusinessIntro? BusinessIntro { get; set; }

    /// <summary>
    /// Optional. For private chats with business accounts, the location of the business.
    /// </summary>
    public BusinessLocation? BusinessLocation { get; set; }

    /// <summary>
    /// Optional. For private chats with business accounts, the opening hours of the business.
    /// </summary>
    public BusinessOpeningHours? BusinessOpeningHours { get; set; }

    /// <summary>
    /// Optional. For private chats, the personal channel of the user.
    /// </summary>
    public Chat? PersonalChat { get; set; }

    /// <summary>
    /// Optional. List of available reactions allowed in the chat. If omitted, then all <see cref="ReactionTypeEmoji.Emoji">emoji reactions</see> are allowed.
    /// </summary>
    public ReactionType[]? AvailableReactions { get; set; }

    /// <summary>
    /// Optional. Custom emoji identifier of emoji chosen by the chat for the reply header and link preview background.
    /// </summary>
    public string? BackgroundCustomEmojiId { get; set; }

    /// <summary>
    /// Optional. Identifier of the accent color for the chat's profile background.
    /// See <see href="https://core.telegram.org/bots/api#profile-accent-colors">profile accent colors</see> for more details.
    /// </summary>
    public int? ProfileAccentColorId { get; set; }

    /// <summary>
    /// Optional. Custom emoji identifier of the emoji chosen by the chat for its profile background.
    /// </summary>
    public string? ProfileBackgroundCustomEmojiId { get; set; }

    /// <summary>
    /// Optional. Custom emoji identifier of emoji status of the other party in a private chat.
    /// </summary>
    public string? EmojiStatusCustomEmojiId { get; set; }

    /// <summary>
    /// Optional. Expiration date of the emoji status of the other party in a private chat, if any.
    /// </summary>
    public DateTime? EmojiStatusExpirationDate { get; set; }

    /// <summary>
    /// Optional. Bio of the other party in a private chat.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if privacy settings of the other party in the private chat allows to use
    /// <c>tg://user?id=&lt;user_id&gt;</c> links only in chats with the user.
    /// </summary>
    public bool HasPrivateForwards { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the privacy settings of the other party restrict sending voice
    /// and video note messages in the private chat.
    /// </summary>
    public bool HasRestrictedVoiceAndVideoMessages { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if users need to join the supergroup before they can send messages.
    /// </summary>
    public bool JoinToSendMessages { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if all users directly joining the supergroup need to be approved by supergroup administrators.
    /// </summary>
    public bool JoinByRequest { get; set; }

    /// <summary>
    /// Optional. Description, for groups, supergroups and channel chats.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional. Primary invite link, for groups, supergroups and channel chats.
    /// </summary>
    public string? InviteLink { get; set; }

    /// <summary>
    /// Optional. The most recent pinned message (by sending date).
    /// </summary>
    public Message? PinnedMessage { get; set; }

    /// <summary>
    /// Optional. Default chat member permissions, for groups and supergroups.
    /// </summary>
    public ChatPermissions? Permissions { get; set; }

    /// <summary>
    /// Optional. For supergroups, the minimum allowed delay between consecutive messages sent by each
    /// </summary>
    public int? SlowModeDelay { get; set; }

    /// <summary>
    /// Optional. For supergroups, the minimum number of boosts that a non-administrator user needs to add in order
    /// to ignore slow mode and chat permissions.
    /// </summary>
    public int? UnrestrictBoostCount { get; set; }

    /// <summary>
    /// Optional. The time after which all messages sent to the chat will be automatically deleted; in seconds.
    /// </summary>
    public int? MessageAutoDeleteTime { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if aggressive anti-spam checks are enabled in the supergroup. The field is
    /// only available to chat administrators.
    /// </summary>
    public bool HasAggressiveAntiSpamEnabled { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if non-administrators can only get the list of bots and administrators in the chat.
    /// </summary>
    public bool HasHiddenMembers { get; set; }

    /// <summary>
    /// Optional.  <see langword="true"/>, if new chat members will have access to old messages; available only to chat administrators.
    /// </summary>
    public bool HasVisibleHistory { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if messages from the chat can't be forwarded to other chats.
    /// </summary>
    public bool HasProtectedContent { get; set; }

    /// <summary>
    /// Optional. For supergroups, name of group sticker set.
    /// </summary>
    public string? StickerSetName { get; set; }

    /// <summary>
    /// Optional. True, if the bot can change the group sticker set.
    /// </summary>
    public bool CanSetStickerSet { get; set; }

    /// <summary>
    /// Optional. For supergroups, the name of the group's custom emoji sticker set. Custom emoji from this set can be
    /// used by all users and bots in the group.
    /// </summary>
    public string? CustomEmojiStickerSetName { get; set; }

    /// <summary>
    /// Optional. Unique identifier for the linked chat, i.e. the discussion group identifier for a channel
    /// and vice versa; for supergroups and channel chats. This identifier may be greater than 32 bits and some
    /// programming languages may have difficulty/silent defects in interpreting it. But it is smaller than
    /// 52 bits, so a signed 64 bit integer or double-precision float type are safe for storing this identifier.
    /// </summary>
    public long? LinkedChatId { get; set; }

    /// <summary>
    /// Optional. For supergroups, the location to which the supergroup is connected.
    /// </summary>
    public ChatLocation? Location { get; set; }
}
