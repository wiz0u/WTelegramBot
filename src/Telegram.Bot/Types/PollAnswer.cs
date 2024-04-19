namespace Telegram.Bot.Types;

/// <summary>
/// This object represents an answer of a user in a non-anonymous poll.
/// </summary>
public partial class PollAnswer
{
    /// <summary>
    /// Unique poll identifier
    /// </summary>
    public string PollId { get; set; } = default!;

    /// <summary>
    /// Optional. The chat that changed the answer to the poll, if the voter is anonymous
    /// </summary>
    public Chat? VoterChat { get; set; }

    /// <summary>
    /// Optional. The user that changed the answer to the poll, if the voter isn't anonymous
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// 0-based identifiers of answer options, chosen by the user. May be empty if the user retracted their vote.
    /// </summary>
    public int[] OptionIds { get; set; } = default!;
}
