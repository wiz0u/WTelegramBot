using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace Telegram.Bot;

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
		[".tgs"] = "application/x-tgsticker",
		[".pdf"] = "application/pdf",
	};

	internal static string GetDisplayName<T>(this T enumValue) where T : Enum
		=> typeof(T).GetMember(enumValue.ToString())[0].GetCustomAttribute<DisplayAttribute>()!.Name!;

	// Task.WhenAll may lead to unnecessary multiple parallel resolve of the same users/stickerset
	internal async static Task<TResult[]> WhenAllSequential<TResult>(this IEnumerable<Task<TResult>> tasks)
	{
		var result = new List<TResult>();
		foreach (var task in tasks)
			result.Add(await task);
		return [.. result];
	}

	internal static string? NullIfEmpty(this string? s) => s == "" ? null : s;

	internal static void ExecuteSave(this DbCommand cmd)
	{
		try
		{
			cmd.ExecuteNonQuery();
		}
		catch (Exception ex)
		{
			WTelegram.Helpers.Log(4, $"{ex.GetType().Name} while saving to DB: {ex.Message} ");
		}
	}

	/// <summary>For serializing indented Json with snake_case fields and enums, and supporting Telegram.Bot polymorphism</summary>
	public static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		Converters = { new JsonStringEnumConverter(SnakeCaseLowerNamingPolicy.Instance), new BotConverter() },
		PropertyNamingPolicy = SnakeCaseLowerNamingPolicy.Instance,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
	};

	private class SnakeCaseLowerNamingPolicy : JsonNamingPolicy
	{
		public static SnakeCaseLowerNamingPolicy Instance = new();
		public override string ConvertName(string name)
		{
			var sb = new StringBuilder();
			bool lowercase = false;
			foreach (var c in name)
			{
				if (lowercase & !(lowercase = char.IsLower(c))) sb.Append('_');
				sb.Append(lowercase ? c : char.ToLower(c));
			}
			return sb.ToString();
		}
	}

	private class BotConverter : JsonConverter<object>
	{
		private static readonly Dictionary<Type, Dictionary<string, Type>> Cache = [];
		public override bool CanConvert(Type typeToConvert)
			=> typeToConvert == typeof(DateTime) || typeToConvert == typeof(TL.MessageEntity) || (typeToConvert.IsAbstract && typeToConvert.Namespace?.StartsWith("Telegram.Bot") == true);
		public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
		{
			if (value is TL.MessageEntity or DateTime)
				JsonSerializer.Serialize(writer, value, WTelegram.Helpers.JsonOptions);
			else
				JsonSerializer.Serialize(writer, value, options);
		}

		public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (typeToConvert == typeof(DateTime) || typeToConvert == typeof(DateTime?))
				return reader.TokenType switch
				{
					JsonTokenType.Null => null,
					JsonTokenType.Number => DateTimeOffset.FromUnixTimeSeconds(reader.GetUInt32()).DateTime,
					_ => JsonSerializer.Deserialize(ref reader, typeToConvert, WTelegram.Helpers.JsonOptions)
				};

			var start = reader;
			if (typeToConvert == typeof(TL.MessageEntity))
			{
				if (reader.TokenType == JsonTokenType.StartObject)
				{
					while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName && reader.GetString() is not "type" and not "Type")
						reader.Skip();
					if (reader.Read() && reader.TokenType == JsonTokenType.String && reader.GetString() is string jsonType)
						{
							if (!Cache.TryGetValue(typeToConvert, out var mapping))
							{
								Cache[typeToConvert] = mapping = [];
								foreach (var field in typeof(MessageEntityType).GetFields())
								{
									var shortName = (string)field.GetRawConstantValue()!;
									var className = "TL.MessageEntity" + shortName;
									mapping[SnakeCaseLowerNamingPolicy.Instance.ConvertName(field.Name)] = mapping[shortName] = typeToConvert.Assembly.GetType(className)!;
								}
							}
							if (!mapping.TryGetValue(jsonType, out var realType))
								throw new JsonException($"Could not determine real subtype of {typeToConvert.FullName} for {jsonType}");
							reader = start;
							return JsonSerializer.Deserialize(ref reader, realType, WTelegram.Helpers.JsonOptions);
						}
				}

				throw new JsonException("Failed to deserialize TL.MessageEntity");
			}
			if (reader.TokenType == JsonTokenType.StartObject)
				if (reader.Read() && reader.TokenType == JsonTokenType.PropertyName && reader.GetString() is string name)
					if (reader.Read() && reader.TokenType == JsonTokenType.String)
					{
						if (!Cache.TryGetValue(typeToConvert, out var mapping))
						{
							var props = typeToConvert.GetProperties(BindingFlags.Public | BindingFlags.Instance);
							var enumProp = props.FirstOrDefault(p => !p.CanWrite && p.GetGetMethod()?.IsAbstract == true);
							var enumType = enumProp?.PropertyType;
							if (enumType?.IsEnum != true) throw new JsonException("Cannot find abstract enum to deserialize " + typeToConvert.FullName);
							var enumValues = enumType.GetEnumValues().Cast<object>().ToDictionary(e => (int)e, e => SnakeCaseLowerNamingPolicy.Instance.ConvertName(e.ToString()!));
							if (name != SnakeCaseLowerNamingPolicy.Instance.ConvertName(enumProp!.Name))
								throw new JsonException($"Enum property name mismatch: {enumProp.Name} / {name}");
							Cache[typeToConvert] = mapping = [];
							var derivedTypes = typeToConvert.Assembly.GetExportedTypes().Where(t => t.BaseType == typeToConvert).ToList();
							foreach (var derived in derivedTypes)
							{
								var getter = derived.GetProperty(enumProp.Name)?.GetMethod;
								var il = getter?.GetMethodBody()?.GetILAsByteArray();
								int value;
								if (il?.Length == 2 && il[0] is >= 0x16 and <= 0x1E)
									value = il[0] - 0x16;
								else
									value = (int)(getter?.Invoke(Activator.CreateInstance(derived), null) ?? throw new JsonException("Cannot determine enum value for " + derived.FullName));
								mapping[enumValues[value]] = derived;
							}
						}
						var jsonType = reader.GetString()!;
						if (!mapping.TryGetValue(jsonType, out var realType))
							throw new JsonException($"Could not determine real subtype of {typeToConvert.FullName} for {jsonType}");
						reader = start;
						return JsonSerializer.Deserialize(ref reader, realType, options);
					}
			throw new JsonException("Failed to deserialize " + typeToConvert.FullName);
		}
	}
}

