// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "<Pending>", Scope = "member", Target = "~T:Telegram.Bot.Types.InlineQueryResults.Documentation")]
[assembly: SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>", Scope = "type", Target = "~T:Telegram.Bot.Types.InlineQueryResults.Documentation")]
[assembly: SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "<Pending>", Scope = "member", Target = "~T:Telegram.Bot.Types.InlineQueryResults.Documentation")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "member", Target = "~T:Telegram.Bot.Types.InlineQueryResults.Documentation")]
[assembly: SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>", Scope = "namespace", Target = "~N:Telegram.Bot.Types.InlineQueryResults")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:Telegram.Bot.TelegramBotClient.GetFileAsync(System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task{Telegram.Bot.Types.File}")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~P:Telegram.Bot.TelegramBotClient.LocalBotServer")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~P:Telegram.Bot.TelegramBotClientOptions.LocalBotServer")]
[assembly: SuppressMessage("Style", "IDE0057:Use range operator", Justification = "<Pending>", Scope = "member", Target = "~M:Telegram.Bot.TelegramBotClientOptions.#ctor(System.String,System.Int32,System.String,System.String,System.Boolean)")]
[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor")]
[assembly: SuppressMessage("Style", "IDE0280:Use 'nameof'")]

namespace System.Runtime.CompilerServices
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal class IsExternalInit { }
}

#pragma warning disable CS9113

// These stubs are just to make the compiler happy with existing Telegram.Bot code, as we are not using Newtonsoft.Json in this package

namespace Newtonsoft.Json
{
	class JsonConstructorAttribute : Attribute;
	class JsonConverterAttribute(Type converterType) : Attribute;
	class JsonObjectAttribute(MemberSerialization _ = default) : Attribute { public Type NamingStrategyType = null!; }
	class JsonPropertyAttribute(string? propertyName = null) : Attribute
	{
		public Required Required { get; set; }
		public DefaultValueHandling DefaultValueHandling { get; set; }
		public NullValueHandling NullValueHandling { get; set; }
		public string? PropertyName { get; set; } = propertyName;
	}
	enum Required { Default, AllowNull, Always, DisallowNull }
	enum DefaultValueHandling { Include, Ignore, Populate, IgnoreAndPopulate }
	enum MemberSerialization { OptOut, OptIn, Fields }
	enum NullValueHandling { Include, Ignore }
	namespace Serialization { class SnakeCaseNamingStrategy; }
	namespace Converters { class UnixDateTimeConverter; }
}
namespace Telegram.Bot.Converters
{
	class BanTimeUnixDateTimeConverter;
	class ChatIdConverter;
	class ChatMemberConverter;
	class InputFileConverter;
	class InputMediaConverter;
	class MenuButtonConverter;
}
namespace Telegram.Bot.Types.Enums
{
	class BotCommandScopeTypeConverter;
	class ChatActionConverter;
	class ChatMemberStatusConverter;
	class ChatTypeConverter;
	class EmojiConverter;
	class FileTypeConverter;
	class InputMediaTypeConverter;
	class MaskPositionPointConverter;
	class MenuButtonTypeConverter;
	class MessageTypeConverter;
	class ParseModeConverter;
	class PollTypeConverter;
	class UpdateTypeConverter;
}
namespace Telegram.Bot.Types.Passport { class EncryptedPassportElementTypeConverter; }
namespace Telegram.Bot.Types.InlineQueryResults { class InlineQueryResultTypeConverter; }
namespace Telegram.Bot.Requests { public class RequestBase<TResponse>(string methodName) : Abstractions.IRequest<TResponse> { } }
namespace Telegram.Bot.Requests.Abstractions { interface IRequest<TResponse> { } }
