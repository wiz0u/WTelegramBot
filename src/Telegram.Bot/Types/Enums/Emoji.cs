using System.ComponentModel.DataAnnotations;

namespace Telegram.Bot.Types.Enums;

/// <summary>
/// Emoji on which the dice throw animation is based
/// <remarks>
/// This enum is used only in the library APIs and is not present in types that are coming from
/// Telegram servers for compatibility reasons
/// </remarks>
/// </summary>
public enum Emoji
{
    /// <summary>
    /// Dice. Resulting value is 1-6
    /// </summary>
    [Display(Name = "🎲")]
    Dice = 1,

    /// <summary>
    /// Darts. Resulting value is 1-6
    /// </summary>
    [Display(Name = "🎯")]
    Darts,

    /// <summary>
    /// Basketball. Resulting value is 1-5
    /// </summary>
    [Display(Name = "🏀")]
    Basketball,

    /// <summary>
    /// Football. Resulting value is 1-5
    /// </summary>
    [Display(Name = "⚽")]
    Football,

    /// <summary>
    /// Slot machine. Resulting value is 1-64
    /// </summary>
    [Display(Name = "🎰")]
    SlotMachine,

    /// <summary>
    /// Bowling. Result value is 1-6
    /// </summary>
    [Display(Name = "🎳")]
    Bowling,
}
