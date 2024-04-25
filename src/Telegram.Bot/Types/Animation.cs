﻿namespace Telegram.Bot.Types;

/// <summary>
/// This object represents an animation file (GIF or H.264/MPEG-4 AVC video without sound).
/// </summary>
public partial class Animation : FileBase
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
    /// Optional. Animation thumbnail as defined by sender
    /// </summary>
    public PhotoSize? Thumbnail { get; set; }

    /// <summary>
    /// Optional. Original animation filename as defined by sender
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Optional. MIME type of the file as defined by sender
    /// </summary>
    public string? MimeType { get; set; }
}