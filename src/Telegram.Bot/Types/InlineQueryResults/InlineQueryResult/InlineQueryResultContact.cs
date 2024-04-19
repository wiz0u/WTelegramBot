using System.Diagnostics.CodeAnalysis;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a contact with a phone number. By default, this contact will be sent by the user.
/// Alternatively, you can use <see cref="InlineQueryResultContact.InputMessageContent"/> to send
/// a message with the specified content instead of the contact.
/// </summary>
public partial class InlineQueryResultContact : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be contact
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Contact;

    /// <summary>
    /// Contact's phone number
    /// </summary>
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Contact's first name
    /// </summary>
    public required string FirstName { get; set; }

    /// <summary>
    /// Optional. Contact's last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Optional. Additional data about the contact in the form of a vCard, 0-2048 bytes
    /// </summary>
    public string? Vcard { get; set; }

    /// <inheritdoc cref="Documentation.InputMessageContent" />
    public InputMessageContent? InputMessageContent { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailUrl" />
    public string? ThumbnailUrl { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailWidth" />
    public int? ThumbnailWidth { get; set; }

    /// <inheritdoc cref="Documentation.ThumbnailHeight" />
    public int? ThumbnailHeight { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="phoneNumber">Contact's phone number</param>
    /// <param name="firstName">Contact's first name</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultContact(string id, string phoneNumber, string firstName)
        : base(id)
    {
        PhoneNumber = phoneNumber;
        FirstName = firstName;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultContact()
    { }
}
