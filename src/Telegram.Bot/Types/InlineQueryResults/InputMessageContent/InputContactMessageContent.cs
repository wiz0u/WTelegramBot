using System.Diagnostics.CodeAnalysis;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents the content of a contact message to be sent as the result of an <see cref="InlineQuery">inline query</see>.
/// </summary>
public partial class InputContactMessageContent : InputMessageContent
{
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

    /// <summary>
    /// Initializes a new input contact message content
    /// </summary>
    /// <param name="phoneNumber">The phone number of the contact</param>
    /// <param name="firstName">The first name of the contact</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InputContactMessageContent(string phoneNumber, string firstName)
    {
        PhoneNumber = phoneNumber;
        FirstName = firstName;
    }

    /// <summary>
    /// Initializes a new input contact message content
    /// </summary>
    public InputContactMessageContent()
    { }
}
