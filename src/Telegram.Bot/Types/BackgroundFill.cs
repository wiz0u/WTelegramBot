namespace Telegram.Bot.Types;

/// <summary>
/// Type of BackgroundFill
/// </summary>
public enum BackgroundFillEnum
{
	/// <summary>
	/// BackgroundFillSolid
	/// </summary>
	Solid = 1,
	/// <summary>
	/// BackgroundFillGradient
	/// </summary>
	Gradient = 2,
	/// <summary>
	/// BackgroundFillFreeformGradient
	/// </summary>
	FreeformGradient = 3,
}

/// <summary>
/// This object describes the way a background is filled based on the selected colors.
/// </summary>
public abstract partial class BackgroundFill
{
	/// <summary>
	/// Type of the background
	/// </summary>
	public abstract BackgroundFillEnum Type { get; }
}

/// <summary>
/// The background is filled using the selected color.
/// </summary>
public partial class BackgroundFillSolid : BackgroundFill
{
	/// <inheritdoc />
	public override BackgroundFillEnum Type => BackgroundFillEnum.Solid;

	/// <summary>
	/// The color of the background fill in the RGB24 format
	/// </summary>
	public int Color { get; set; }
}

/// <summary>
/// The background is a gradient fill.
/// </summary>
public partial class BackgroundFillGradient : BackgroundFill
{
	/// <inheritdoc />
	public override BackgroundFillEnum Type => BackgroundFillEnum.Gradient;

	/// <summary>
	/// Top color of the gradient in the RGB24 format
	/// </summary>
	public int TopColor { get; set; }

	/// <summary>
	/// Bottom color of the gradient in the RGB24 format
	/// </summary>
	public int BottomColor { get; set; }

	/// <summary>
	/// Clockwise rotation angle of the background fill in degrees; 0-359
	/// </summary>
	public int RotationAngle { get; set; }
}

/// <summary>
/// The background is a freeform gradient that rotates after every message in the chat.
/// </summary>
public partial class BackgroundFillFreeformGradient : BackgroundFill
{
	/// <inheritdoc />
	public override BackgroundFillEnum Type => BackgroundFillEnum.FreeformGradient;

	/// <summary>
	/// A list of the 3 or 4 base colors that are used to generate the freeform gradient in the RGB24 format
	/// </summary>
	public int[] Colors { get; set; } = default!;
}
