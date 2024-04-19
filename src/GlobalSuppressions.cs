// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

global using Newtonsoft.Json;
global using Newtonsoft.Json.Serialization;
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
[assembly: SuppressMessage("Style", "IDE0057:Use range operator", Justification = "<Pending>", Scope = "member", Target = "~T:Telegram.Bot.TelegramBotClientOptions")]

#pragma warning disable CS9113, CS1591, CA1018

namespace System.Runtime.CompilerServices
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal class IsExternalInit { }

#if NETSTANDARD2_1
	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute { }
#endif
}

// These stubs are just to make the compiler happy with existing Telegram.Bot code, as we are not using Newtonsoft.Json in this package

namespace JetBrains.Annotations { class PublicAPIAttribute : Attribute; }
namespace Newtonsoft.Json
{
	class JsonConstructorAttribute : Attribute;
	class JsonConverterAttribute(Type converterType) : Attribute;
	class JsonIgnoreAttribute : Attribute;
	class JsonObjectAttribute(MemberSerialization memberSerialization = default) : Attribute { public Type NamingStrategyType = null!; }
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
	class UnixDateTimeConverter;
}
namespace Newtonsoft.Json.Converters { }
namespace Newtonsoft.Json.Serialization
{
	class SnakeCaseNamingStrategy;
}
namespace Telegram.Bot.Converters
{
	class BanTimeUnixDateTimeConverter;
	class ChatIdConverter;
	class ChatMemberConverter;
	class ColorConverter;
	class InputFileConverter;
	class InputMediaConverter;
	class MenuButtonConverter;
	class MessageOriginConverter;
	class ReactionTypeConverter;
	class ChatBoostSourceConverter;
	class MaybeInaccessibleMessageConverter;
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
	class StickerFormatConverter;
	class StickerTypeConverter;
	class UpdateTypeConverter;
	class ChatBoostSourceTypeConverter;
	class MessageOriginTypeConverter;
	class ReactionTypeKindConverter;
}
namespace Telegram.Bot.Types.Passport { class EncryptedPassportElementTypeConverter; }
namespace Telegram.Bot.Types.InlineQueryResults { class InlineQueryResultTypeConverter; }
namespace Telegram.Bot.Requests { public class RequestBase<TResponse>(string methodName) : Abstractions.IRequest<TResponse> { } }
namespace Telegram.Bot.Requests.Abstractions { interface IRequest<TResponse> { } }
