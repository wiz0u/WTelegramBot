﻿namespace Telegram.Bot.Types;

/// <summary>This object represents a voice note.</summary>
public partial class Voice : FileBase
{
    /// <summary>Duration of the audio in seconds as defined by sender</summary>
    public int Duration { get; set; }

    /// <summary><em>Optional</em>. MIME type of the file as defined by sender</summary>
    public string? MimeType { get; set; }
}
