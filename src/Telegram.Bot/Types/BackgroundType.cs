namespace Telegram.Bot.Types;

/// <summary>
/// This object represents a chat background.
/// </summary>
public partial class ChatBackground
{
	/// <summary>
	/// Type of the background
	/// </summary>
	public BackgroundType Type { get; set; } = default!;
}

/// <summary>
/// Type of background
/// </summary>
public enum BackgroundTypeEnum
{
	/// <summary>
	/// BackgroundTypeFill
	/// </summary>
	Fill = 1,
	/// <summary>
	/// BackgroundTypeWallpaper
	/// </summary>
	Wallpaper = 2,
	/// <summary>
	/// BackgroundTypePattern
	/// </summary>
	Pattern = 3,
	/// <summary>
	/// BackgroundTypeChatTheme
	/// </summary>
	ChatTheme = 4,
}

/// <summary>
/// This object describes the type of a background.Currently, it can be one of
/// </summary>
public abstract partial class BackgroundType
{
	/// <summary>
	/// Type of the background
	/// </summary>
	public abstract BackgroundTypeEnum Type { get; }
}

/// <summary>
/// The background is automatically filled based on the selected colors.
/// </summary>
public partial class BackgroundTypeFill : BackgroundType
{
	/// <inheritdoc />
	public override BackgroundTypeEnum Type => BackgroundTypeEnum.Fill;

	/// <summary>
	/// The background fill
	/// </summary>
	public BackgroundFill Fill { get; set; } = default!;

	/// <summary>
	/// Dimming of the background in dark themes, as a percentage; 0-100
	/// </summary>
	public int DarkThemeDimming { get; set; }
}

/// <summary>
/// The background is a wallpaper in the JPEG format.
/// </summary>
public partial class BackgroundTypeWallpaper : BackgroundType
{
	/// <inheritdoc />
	public override BackgroundTypeEnum Type => BackgroundTypeEnum.Wallpaper;

	/// <summary>
	/// Document with the wallpaper
	/// </summary>
	public Document Document { get; set; } = default!;

	/// <summary>
	/// Dimming of the background in dark themes, as a percentage; 0-100
	/// </summary>
	public int DarkThemeDimming { get; set; }

	/// <summary>
	/// Optional. True, if the wallpaper is downscaled to fit in a 450x450 square and then box-blurred with radius 12
	/// </summary>
	public bool? IsBlurred { get; set; }

	/// <summary>
	/// Optional. True, if the background moves slightly when the device is tilted
	/// </summary>
	public bool? IsMoving { get; set; }
}

/// <summary>
/// The background is a PNG or TGV(gzipped subset of SVG with MIME type “application/x-tgwallpattern”) pattern to be combined with the background fill chosen by the user.
/// </summary>
public partial class BackgroundTypePattern : BackgroundType
{
	/// <inheritdoc />
	public override BackgroundTypeEnum Type => BackgroundTypeEnum.Pattern;

	/// <summary>
	/// Document with the pattern
	/// </summary>
	public Document Document { get; set; } = default!;

	/// <summary>
	/// The background fill that is combined with the pattern
	/// </summary>
	public BackgroundFill Fill { get; set; } = default!;

	/// <summary>
	/// Intensity of the pattern when it is shown above the filled background; 0-100
	/// </summary>
	public int Intensity { get; set; }

	/// <summary>
	/// Optional. True, if the background fill must be applied only to the pattern itself.All other pixels are black in this case. For dark themes only
	/// </summary>
	public bool? IsInverted { get; set; }

	/// <summary>
	/// Optional. True, if the background moves slightly when the device is tilted
	/// </summary>
	public bool? IsMoving { get; set; }
}

/// <summary>
/// The background is taken directly from a built-in chat theme.
/// </summary>
public partial class BackgroundTypeChatTheme : BackgroundType
{
	/// <inheritdoc />
	public override BackgroundTypeEnum Type => BackgroundTypeEnum.ChatTheme;

	/// <summary>
	/// Name of the chat theme, which is usually an emoji
	/// </summary>
	public string ThemeName { get; set; } = default!;
}
