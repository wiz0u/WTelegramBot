using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a file that is already stored somewhere on the Telegram servers
/// </summary>
public partial class InputFileId : InputFile
{
    /// <inheritdoc/>
    public override FileType FileType => FileType.Id;

    /// <summary>
    /// A file identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// This object represents a file that is already stored somewhere on the Telegram servers
    /// </summary>
    public InputFileId()
    {}

    /// <summary>
    /// This object represents a file that is already stored somewhere on the Telegram servers
    /// </summary>
    /// <param name="id">A file identifier</param>
    [SetsRequiredMembers]
    public InputFileId(string id)
        => Id = id;
}
