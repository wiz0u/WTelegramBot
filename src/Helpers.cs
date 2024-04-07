using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Telegram.Bot;

public static class Helpers
{
	public static readonly Dictionary<string, string> ExtToMimeType = new(StringComparer.OrdinalIgnoreCase)
	{
		[".jpg"] = "image/jpeg",
		[".jpeg"] = "image/jpeg",
		[".png"] = "image/png",
		[".bmp"] = "image/bmp",
		[".gif"] = "image/gif",
		[".webp"] = "image/webp",
		[".webm"] = "video/webm",
		[".mp4"] = "video/mp4",
		[".mov"] = "video/quicktime",
		[".avi"] = "video/x-msvideo",
		[".aac"] = "audio/aac",
		[".mid"] = "audio/midi",
		[".midi"] = "audio/midi",
		[".ogg"] = "audio/ogg",
		[".mp3"] = "audio/mpeg",
		[".wav"] = "audio/x-wav",
		[".tgs"] = "application/x-tgsticker",
		[".pdf"] = "application/pdf",
	};
	public static Func<string, string?> GetMimeType { get; set; } = ExtToMimeType.GetValueOrDefault;

	public static string GetDisplayName<T>(this T enumValue) where T : Enum
		=> typeof(T).GetMember(enumValue.ToString())[0].GetCustomAttribute<DisplayAttribute>()!.Name!;
}

