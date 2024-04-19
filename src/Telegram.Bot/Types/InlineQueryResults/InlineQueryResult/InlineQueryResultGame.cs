using System.Diagnostics.CodeAnalysis;

namespace Telegram.Bot.Types.InlineQueryResults;

/// <summary>
/// Represents a <see cref="Game"/>.
/// </summary>
public partial class InlineQueryResultGame : InlineQueryResult
{
    /// <summary>
    /// Type of the result, must be game
    /// </summary>
    public override InlineQueryResultType Type => InlineQueryResultType.Game;

    /// <summary>
    /// Short name of the game
    /// </summary>
    public required string GameShortName { get; set; }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    /// <param name="id">Unique identifier of this result</param>
    /// <param name="gameShortName">Short name of the game</param>
    [SetsRequiredMembers]
    [Obsolete("Use parameterless constructor with required properties")]
    public InlineQueryResultGame(string id, string gameShortName)
        : base(id)
    {
        GameShortName = gameShortName;
    }

    /// <summary>
    /// Initializes a new inline query result
    /// </summary>
    public InlineQueryResultGame()
    { }
}
