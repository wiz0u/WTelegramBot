namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a video file.
/// </summary>
public partial class Video : FileBase
{
    /// <summary>
    /// Video width as defined by sender
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Video height as defined by sender
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Duration of the video in seconds as defined by sender
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Optional. Video thumbnail
    /// </summary>
    public PhotoSize? Thumbnail { get; set; }

    /// <summary>
    /// Optional. Original filename as defined by sender
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Optional. Mime type of a file as defined by sender
    /// </summary>
    public string? MimeType { get; set; }
}
