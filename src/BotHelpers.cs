using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Reflection;
using System.Text;

namespace WTelegram;

/// <summary>Helpers methods</summary>
public static class BotHelpers
{
	/// <summary>Used to guess MimeType based on file extension, when uploading documents</summary>
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
		[".flac"] = "audio/flac",
		[".tgs"] = "application/x-tgsticker",
		[".pdf"] = "application/pdf",
	};

	// Task.WhenAll may lead to unnecessary multiple parallel resolve of the same users/stickerset
	internal async static Task<TResult[]> WhenAllSequential<TResult>(this IEnumerable<Task<TResult>> tasks)
	{
		var result = new List<TResult>();
		foreach (var task in tasks)
			result.Add(await task);
		return [.. result];
	}

	internal static string? NullIfEmpty(this string? s) => s == "" ? null : s;
	internal static int? NullIfZero(this int i) => i == 0 ? null : i;
	internal static DateTime? NullIfDefault(this DateTime d) => d == default ? null : d;
	internal static string? NullOrUtf8(this byte[] b) => b == null ? null : Encoding.UTF8.GetString(b);
	internal static int? IntIfPositive(this long l) => l > 0 ? checked((int)l) : null;

	internal static void ExecuteSave(this DbCommand cmd)
	{
		try
		{
			cmd.ExecuteNonQuery();
		}
		catch (Exception ex)
		{
			bool retrying = cmd.Connection!.State == System.Data.ConnectionState.Closed;
			Helpers.Log(retrying ? 3 : 4, $"{ex.GetType().Name} while saving to DB{(retrying ? " (will retry)" : "")}: {ex.Message}");
			if (retrying)
			{
				cmd.Connection.Open();
				try
				{
					cmd.ExecuteNonQuery();
				}
				catch (Exception)
				{
					Helpers.Log(4, $"{ex.GetType().Name} while saving to DB again: {ex.Message}");
				}
			}
		}
	}
}
