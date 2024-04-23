namespace Telegram.Bot.Types;

/// <summary>
/// Represents the rights of an administrator in a chat.
/// </summary>
public partial class ChatAdministratorRights
{
    /// <summary>
    /// <see langword="true"/>, if the user's presence in the chat is hidden
    /// </summary>
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the administrator can access the chat event log, chat statistics, message statistics in
    /// channels, see channel members, see anonymous administrators in supergroups and ignore slow mode.
    /// Implied by any other administrator privilege
    /// </summary>
    public bool CanManageChat { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the administrator can delete messages of other users
    /// </summary>
    public bool CanDeleteMessages { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the administrator can manage video chats
    /// </summary>
    public bool CanManageVideoChats { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the administrator can restrict, ban or unban chat members
    /// </summary>
    public bool CanRestrictMembers { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the administrator can add new administrators with a subset of their own privileges or demote
    /// administrators that he has promoted, directly or indirectly (promoted by administrators that were appointed
    /// by the user)
    /// </summary>
    public bool CanPromoteMembers { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the user is allowed to change the chat title, photo and other settings
    /// </summary>
    public bool CanChangeInfo { get; set; }

    /// <summary>
    /// <see langword="true"/>, if the user is allowed to invite new users to the chat
    /// </summary>
    public bool CanInviteUsers { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the administrator can post in the channel; channels only
    /// </summary>
    public bool CanPostMessages { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the administrator can edit messages of other users and can pin messages;
    /// channels only
    /// </summary>
    public bool CanEditMessages { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the user is allowed to pin messages; groups and supergroups only
    /// </summary>
    public bool CanPinMessages { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the administrator can post stories in the channel; channels only
    /// </summary>
    public bool CanPostStories { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the administrator can edit stories posted by other users; channels only
    /// </summary>
    public bool CanEditStories { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the administrator can delete stories posted by other users; channels only
    /// </summary>
    public bool CanDeleteStories { get; set; }

    /// <summary>
    /// Optional. <see langword="true"/>, if the user is allowed to create, rename, close, and reopen forum topics; supergroups only
    /// </summary>
    public bool CanManageTopics { get; set; }
}
