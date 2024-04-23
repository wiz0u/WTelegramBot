using Telegram.Bot.Types.Enums;


#pragma warning disable CS9113
namespace Telegram.Bot.Requests
{
	/// <summary>Represents an API request</summary>
	public class RequestBase<TResponse>(string methodName) : Abstractions.IRequest<TResponse> { }
}
namespace Telegram.Bot.Requests.Abstractions
{
	interface IRequest<TResponse> { }
}
#pragma warning restore CS9113

namespace Telegram.Bot.Types
{
	public partial class Update
	{
		/// <summary>The corresponding Client API update structure</summary>
		public TL.Update? RawUpdate;
	}

	public partial class Chat
	{
		/// <summary>Client API access_hash of the chat</summary>
		public long AccessHash { get; set; }
		/// <summary>Useful operator for Client API calls</summary>
		public static implicit operator TL.InputPeer(Chat chat) => chat.Type switch
		{
			ChatType.Private => new TL.InputPeerUser(-chat.Id, chat.AccessHash),
			ChatType.Group => new TL.InputPeerChat(-chat.Id),
			_ => new TL.InputPeerChannel(-1000000000000 - chat.Id, chat.AccessHash),
		};
	}

	public partial class User
	{
		/// <summary>Client API access_hash of the user</summary>
		public long AccessHash { get; set; }
		/// <summary>Useful operator for Client API calls</summary>
		[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(user))]
		public static implicit operator TL.InputPeerUser?(User? user) => user == null ? null : new(user.Id, user.AccessHash);
		/// <summary>Useful operator for Client API calls</summary>
		[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(user))]
		public static implicit operator TL.InputUser?(User? user) => user == null ? null : new(user.Id, user.AccessHash);
	}

	public partial class InputFile
	{
		/// <summary>Implicit operator, same as <see cref="InputFile.FromStream"/></summary>
		public static implicit operator InputFile(Stream stream) => FromStream(stream);
		/// <summary>Implicit operator, same as <see cref="InputFile.FromString"/></summary>
		public static implicit operator InputFile(string urlOrFileId) => FromString(urlOrFileId);
	}

	public partial class InputFileId
	{
		/// <summary>Implicit operator, same as <see cref="InputFileId(string)"/></summary>
		public static implicit operator InputFileId(string fileId) => new(fileId);
	}

	public partial class InputFileStream
	{
		/// <summary>Implicit operator, same as <see cref="InputFileStream(Stream)"/></summary>
		public static implicit operator InputFileStream(Stream stream) => new(stream);
	}

	/// <summary>Type of a <see cref="MessageEntity"/></summary>
	public static class MessageEntityType // litterals are suffix of MessageEntity* classes
	{
		/// <summary>A mentioned <see cref="User"/></summary>
		public const string Mention = "Mention";
		/// <summary>A searchable Hashtag</summary>
		public const string Hashtag = "Hashtag";
		/// <summary>A Bot command</summary>
		public const string BotCommand = "BotCommand";
		/// <summary>An URL</summary>
		public const string Url = "Url";
		/// <summary>An email</summary>
		public const string Email = "Email";
		/// <summary>Bold text</summary>
		public const string Bold = "Bold";
		/// <summary>Italic text</summary>
		public const string Italic = "Italic";
		/// <summary>Monowidth string</summary>
		public const string Code = "Code";
		/// <summary>Monowidth block</summary>
		public const string Pre = "Pre";
		/// <summary>Clickable text URLs</summary>
		public const string TextLink = "TextUrl";
		/// <summary>Mentions for a <see cref="User"/> without <see cref="User.Username"/></summary>
		public const string TextMention = "MentionName";
		/// <summary>Phone number</summary>
		public const string PhoneNumber = "Phone";
		/// <summary>A cashtag (e.g. $EUR, $USD) - $ followed by the short currency code</summary>
		public const string Cashtag = "Cashtag";
		/// <summary>Underlined text</summary>
		public const string Underline = "Underline";
		/// <summary>Strikethrough text</summary>
		public const string Strikethrough = "Strike";
		/// <summary>Spoiler message</summary>
		public const string Spoiler = "Spoiler";
		/// <summary>Inline custom emoji stickers</summary>
		public const string CustomEmoji = "CustomEmoji";
		/// <summary>Block quotation</summary>
		public const string Blockquote = "Blockquote";
	}

	public partial class ReplyParameters
	{
		/// <summary>Implicit operator when you just want to reply to a message in same chat</summary>
		public static implicit operator ReplyParameters(int replyToMessageId) => new() { MessageId = replyToMessageId };
	}

	public partial class MessageId
	{
		/// <summary>Implicit operator to int</summary>
		public static implicit operator int(MessageId msgID) => msgID.Id;
		/// <summary>Implicit operator from int</summary>
		public static implicit operator MessageId(int id) => new() { Id = id };
		/// <summary>Implicit operator from Message</summary>
		public static implicit operator MessageId(Message msg) => new() { Id = msg.MessageId };
	}

	public abstract partial class ReactionType
	{
		/// <summary>Implicit operator ReactionTypeEmoji from string</summary>
		public static implicit operator ReactionType(string emoji) => new ReactionTypeEmoji { Emoji = emoji };
		/// <summary>Implicit operator ReactionTypeCustomEmoji from long customEmojiId</summary>
		public static implicit operator ReactionType(long customEmojiId) => new ReactionTypeCustomEmoji { CustomEmojiId = customEmojiId.ToString() };
	}

	public partial class LinkPreviewOptions
	{
		/// <summary>To get the same behaviour as previous parameter <c>disableWebPagePreview:</c></summary>
		public static implicit operator LinkPreviewOptions(bool disabled) => new() { IsDisabled = disabled };
	}
}
