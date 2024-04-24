using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Types;

/// <summary>
/// Represents an audio file to be treated as music to be sent.
/// </summary>
public partial class InputMediaAudio :
    InputMedia,
    IInputMediaThumb,
    IAlbumInputMedia
{
    /// <inheritdoc />
    public override InputMediaType Type => InputMediaType.Audio;

    /// <inheritdoc />
    public InputFile? Thumbnail { get; set; }

    /// <summary>
    /// Optional. Duration of the audio in seconds
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Optional. Performer of the audio
    /// </summary>
    public string? Performer { get; set; }

    /// <summary>
    /// Optional. Title of the audio
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Initializes a new audio media to send with an <see cref="InputFile"/>
    /// </summary>
    /// <param name="media">File to send</param>
    [SetsRequiredMembers]
    public InputMediaAudio(InputFile media)
        : base(media)
    { }

    /// <summary>
    /// Initializes a new audio media to send with an <see cref="InputFile"/>
    /// </summary>
    public InputMediaAudio()
    { }
}
