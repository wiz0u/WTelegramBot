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
		public TL.Update? TLUpdate;
	}

	public partial class Message
	{
		/// <summary>The corresponding Client API message structure</summary>
		public TL.MessageBase? TLMessage;


		/// <summary>
		/// <em>Optional</em>. For forwarded messages, sender of the original message
		/// </summary>
		public User? ForwardFrom => (ForwardOrigin as MessageOriginUser)?.SenderUser;

		/// <summary>
		/// <em>Optional</em>. For messages forwarded from channels or from anonymous administrators, information about the
		/// original sender chat
		/// </summary>
		public Chat? ForwardFromChat => ForwardOrigin switch
		{
			MessageOriginChannel originChannel => originChannel.Chat,
			MessageOriginChat originChat => originChat.SenderChat,
			_ => null,
		};

		/// <summary>
		/// <em>Optional</em>. For messages forwarded from channels, identifier of the original message in the channel
		/// </summary>
		public int? ForwardFromMessageId => (ForwardOrigin as MessageOriginChannel)?.MessageId;

		/// <summary>
		/// <em>Optional</em>. For messages forwarded from channels, signature of the post author if present
		/// </summary>
		public string? ForwardSignature => (ForwardOrigin as MessageOriginChannel)?.AuthorSignature;

		/// <summary>
		/// <em>Optional</em>. Sender's name for messages forwarded from users who disallow adding a link to their account in
		/// forwarded messages
		/// </summary>
		public string? ForwardSenderName => (ForwardOrigin as MessageOriginHiddenUser)?.SenderUserName;

		/// <summary>
		/// <em>Optional</em>. For forwarded messages, date the original message was sent
		/// </summary>
		public DateTime? ForwardDate => ForwardOrigin?.Date;

		/// <summary>
		/// Gets the entity values.
		/// </summary>
		/// <value>
		/// The entity contents.
		/// </value>
		public IEnumerable<string>? EntityValues =>
			Text is null
				? default
				: Entities?.Select(entity => Text.Substring(entity.Offset, entity.Length));

		/// <summary>
		/// Gets the caption entity values.
		/// </summary>
		/// <value>
		/// The caption entity contents.
		/// </value>
		public IEnumerable<string>? CaptionEntityValues =>
			Caption is null
				? default
				: CaptionEntities?.Select(entity => Caption.Substring(entity.Offset, entity.Length));
	}

	public partial class Chat
	{
		/// <summary>The corresponding Client API message structure. Real type can be TL.User, TL.Chat, TL.Channel...<br/>
		/// For ChatFullInfo, real type can be TL.Users_UserFull, TL.Messages_ChatFull)</summary>
		public TL.IObject? TLInfo;

		/// <summary>Client API access_hash of the chat</summary>
		public long AccessHash { get; set; }
		/// <summary>Useful operator for Client API calls</summary>
		public static implicit operator TL.InputPeer(Chat chat) => chat.Type switch
		{
			ChatType.Private => new TL.InputPeerUser(chat.Id, chat.AccessHash),
			ChatType.Group => new TL.InputPeerChat(-chat.Id),
			_ => new TL.InputPeerChannel(-1000000000000 - chat.Id, chat.AccessHash),
		};
	}

	public partial class User
	{
		/// <summary>The corresponding Client API message structure</summary>
		public TL.User? TLUser;
		/// <summary>Client API access_hash of the user</summary>
		public long AccessHash { get; set; }
		/// <summary>Useful operator for Client API calls</summary>
		[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(user))]
		public static implicit operator TL.InputPeerUser?(User? user) => user == null ? null : new(user.Id, user.AccessHash);
		/// <summary>Useful operator for Client API calls</summary>
		[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(user))]
		public static implicit operator TL.InputUser?(User? user) => user == null ? null : new(user.Id, user.AccessHash);

		/// <inheritdoc/>
		public override string ToString() =>
			$"{(Username is null ? $"{FirstName}{LastName?.Insert(0, " ")}" : $"@{Username}")} ({Id})";
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

	public partial class BotName
	{
		/// <summary>implicit to string</summary>
		public static implicit operator string(BotName bn) => bn.Name;
		/// <summary>implicit from string</summary>
		public static implicit operator BotName(string bn) => new() { Name = bn };
	}

	public partial class BotShortDescription
	{
		/// <summary>implicit to string</summary>
		public static implicit operator string(BotShortDescription bsd) => bsd.ShortDescription;
		/// <summary>implicit from string</summary>
		public static implicit operator BotShortDescription(string bsd) => new() { ShortDescription = bsd };
	}
	
	public partial class BotDescription
	{
		/// <summary>implicit to string</summary>
		public static implicit operator string(BotDescription bd) => bd.Description;
		/// <summary>implicit from string</summary>
		public static implicit operator BotDescription(string bd) => new() { Description = bd };
	}

	public partial class WebAppInfo
	{
		/// <summary>implicit to string (URL)</summary>
		public static implicit operator string(WebAppInfo wai) => wai.Url;
		/// <summary>implicit from string (URL)</summary>
		public static implicit operator WebAppInfo(string url) => new() { Url = url };
	}

	public partial class InputPollOption
	{
		/// <summary>Implicit operator, same as <see cref="InputFileId(string)"/></summary>
		public static implicit operator InputPollOption(string text) => new() { Text = text };
	}

	public partial class StickerSet
	{
		/// <summary>
		/// <see langword="true"/>, if the sticker set contains <see cref="StickerFormat.Animated">animated stickers</see>
		/// </summary>
		[Obsolete("This field is no longer sent by Bot API")]
		public bool IsAnimated { get; set; }

		/// <summary>
		/// <see langword="true"/>, if the sticker set contains <see cref="StickerFormat.Video">video stickers</see>
		/// </summary>
		[Obsolete("This field is no longer sent by Bot API")]
		public bool IsVideo { get; set; }
	}

	public partial class BotCommandScope
	{
		/// <summary>
		/// Create a default <see cref="BotCommandScope"/> instance
		/// </summary>
		/// <returns></returns>
		public static BotCommandScopeDefault Default() => new();

		/// <summary>
		/// Create a <see cref="BotCommandScope"/> instance for all private chats
		/// </summary>
		/// <returns></returns>
		public static BotCommandScopeAllPrivateChats AllPrivateChats() => new();

		/// <summary>
		/// Create a <see cref="BotCommandScope"/> instance for all group chats
		/// </summary>
		public static BotCommandScopeAllGroupChats AllGroupChats() => new();

		/// <summary>
		/// Create a <see cref="BotCommandScope"/> instance for all chat administrators
		/// </summary>
		public static BotCommandScopeAllChatAdministrators AllChatAdministrators() =>
			new();

		/// <summary>
		/// Create a <see cref="BotCommandScope"/> instance for a specific <see cref="Chat"/>
		/// </summary>
		/// <param name="chatId">
		/// Unique identifier for the target <see cref="Chat"/> or username of the target supergroup
		/// </param>
		public static BotCommandScopeChat Chat(ChatId chatId) => new() { ChatId = chatId };

		/// <summary>
		/// Create a <see cref="BotCommandScope"/> instance for a specific member in a specific <see cref="Chat"/>
		/// </summary>
		/// <param name="chatId">
		/// Unique identifier for the target <see cref="Chat"/> or username of the target supergroup
		/// </param>
		public static BotCommandScopeChatAdministrators ChatAdministrators(ChatId chatId) =>
			new() { ChatId = chatId };

		/// <summary>
		/// Represents the <see cref="BotCommandScope">scope</see> of bot commands, covering a specific member of a group or supergroup chat.
		/// </summary>
		/// <param name="chatId">
		/// Unique identifier for the target <see cref="Chat"/> or username of the target supergroup
		/// </param>
		/// <param name="userId">Unique identifier of the target user</param>
		public static BotCommandScopeChatMember ChatMember(ChatId chatId, long userId) =>
			new() { ChatId = chatId, UserId = userId };
	}

	public partial class CallbackQuery
	{
		/// <summary>
		/// Indicates if the User requests a Game
		/// </summary>
		public bool IsGameQuery => GameShortName != default;
	}

	public partial class InputSticker
	{
		/// <summary>
		/// Initializes a new input sticker to create or add sticker sets
		/// with an <see cref="InputFile">sticker</see> and emojiList
		/// </summary>
		/// <param name="sticker">
		/// The added sticker. Pass a file_id as a String to send a file that already exists
		/// on the Telegram servers, pass an HTTP URL as a String for Telegram to get a file
		/// from the Internet, or upload a new one using multipart/form-data.
		/// <see cref="StickerFormat.Animated">Animated</see> and <see cref="StickerFormat.Video">video</see>
		/// stickers can't be uploaded via HTTP URL.
		/// </param>
		/// <param name="emojiList">
		/// List of 1-20 emoji associated with the sticker
		/// </param>
		/// <param name="format">Format of the added sticker</param>
		[SetsRequiredMembers]
		public InputSticker(InputFile sticker, IEnumerable<string> emojiList, StickerFormat format)
		{
			Format = format;
			Sticker = sticker;
			EmojiList = emojiList.ToArray();
		}

		/// <summary>
		/// Initializes a new input sticker to create or add sticker sets
		/// </summary>
		public InputSticker()
		{ }
	}

	namespace ReplyMarkups
	{
		public partial class ReplyKeyboardMarkup
		{
			/// <summary>
			/// Initializes a new instance of <see cref="ReplyKeyboardMarkup"/> with one button
			/// </summary>
			/// <param name="button">Button on keyboard</param>
			[SetsRequiredMembers]
			public ReplyKeyboardMarkup(KeyboardButton button)
				: this([button])
			{ }

			/// <summary>
			/// Initializes a new instance of <see cref="ReplyKeyboardMarkup"/>
			/// </summary>
			/// <param name="keyboardRow">The keyboard row.</param>
			[SetsRequiredMembers]
			public ReplyKeyboardMarkup(IEnumerable<KeyboardButton> keyboardRow)
				: this([keyboardRow])
			{ }

			/// <summary>
			/// Generates a reply keyboard markup with one button
			/// </summary>
			/// <param name="text">Button's text</param>
			public static implicit operator ReplyKeyboardMarkup?(string? text) =>
				text is null
					? default
					: new([new KeyboardButton(text)]);

			/// <summary>
			/// Generates a reply keyboard markup with multiple buttons on one row
			/// </summary>
			/// <param name="texts">Texts of buttons</param>
			public static implicit operator ReplyKeyboardMarkup?(string[]? texts) =>
				texts is null
					? default
					: new[] { texts };

			/// <summary>
			/// Generates a reply keyboard markup with multiple buttons
			/// </summary>
			/// <param name="textsItems">Texts of buttons</param>
			public static implicit operator ReplyKeyboardMarkup?(string[][]? textsItems) =>
				textsItems is null
					? default
					: new ReplyKeyboardMarkup(
						textsItems.Select(texts =>
							texts.Select(t => new KeyboardButton(t))
						));
		}

		public partial class InlineKeyboardMarkup
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="InlineKeyboardMarkup"/> class with only one keyboard button
			/// </summary>
			/// <param name="inlineKeyboardButton">Keyboard button</param>
			[SetsRequiredMembers]
			public InlineKeyboardMarkup(InlineKeyboardButton inlineKeyboardButton)
				: this([inlineKeyboardButton])
			{ }

			/// <summary>
			/// Initializes a new instance of the <see cref="InlineKeyboardMarkup"/> class with a one-row keyboard
			/// </summary>
			/// <param name="inlineKeyboardRow">The inline keyboard row</param>
			[SetsRequiredMembers]
			public InlineKeyboardMarkup(IEnumerable<InlineKeyboardButton> inlineKeyboardRow)
				: this([inlineKeyboardRow])
			{ }

			/// <summary>
			/// Generate an empty inline keyboard markup
			/// </summary>
			/// <returns>Empty inline keyboard markup</returns>
			public static InlineKeyboardMarkup Empty() =>
				new(Array.Empty<InlineKeyboardButton[]>());

			/// <summary>
			/// Generate an inline keyboard markup with one button
			/// </summary>
			/// <param name="button">Inline keyboard button</param>
			[return: NotNullIfNotNull(nameof(button))]
			public static implicit operator InlineKeyboardMarkup?(InlineKeyboardButton? button) =>
				button is null ? default : new(button);

			/// <summary>
			/// Generate an inline keyboard markup with one button
			/// </summary>
			/// <param name="buttonText">Text of the button</param>
			[return: NotNullIfNotNull(nameof(buttonText))]
			public static implicit operator InlineKeyboardMarkup?(string? buttonText) =>
				buttonText is null ? default : new(buttonText!);

			/// <summary>
			/// Generate an inline keyboard markup from multiple buttons
			/// </summary>
			/// <param name="inlineKeyboard">Keyboard buttons</param>
			[return: NotNullIfNotNull(nameof(inlineKeyboard))]
			public static implicit operator InlineKeyboardMarkup?(IEnumerable<InlineKeyboardButton>[]? inlineKeyboard) =>
				inlineKeyboard is null ? default : new(inlineKeyboard);

			/// <summary>
			/// Generate an inline keyboard markup from multiple buttons on 1 row
			/// </summary>
			/// <param name="inlineKeyboard">Keyboard buttons</param>
			[return: NotNullIfNotNull(nameof(inlineKeyboard))]
			public static implicit operator InlineKeyboardMarkup?(InlineKeyboardButton[]? inlineKeyboard) =>
				inlineKeyboard is null ? default : new(inlineKeyboard);
		}

		public partial class InlineKeyboardButton
		{
			/// <summary>
			/// Creates an inline keyboard button that sends <see cref="CallbackQuery"/> to bot when pressed
			/// </summary>
			/// <param name="textAndCallbackData">
			/// Text and data of the button to be sent in a <see cref="CallbackQuery">callback query</see> to the bot when
			/// button is pressed, 1-64 bytes
			/// </param>
			public static InlineKeyboardButton WithCallbackData(string textAndCallbackData) =>
				new(textAndCallbackData) { CallbackData = textAndCallbackData };
		}

		public partial class InlineKeyboardButton
		{
			/// <summary>
			/// Performs an implicit conversion from <see cref="string"/> to <see cref="InlineKeyboardButton"/>
			/// with callback data
			/// </summary>
			/// <param name="textAndCallbackData">Label text and callback data of the button</param>
			/// <returns>
			/// The result of the conversion.
			/// </returns>
			public static implicit operator InlineKeyboardButton?(string? textAndCallbackData) =>
				textAndCallbackData is null
					? default
					: WithCallbackData(textAndCallbackData);
		}
		
		public partial class KeyboardButton
		{
			/// <summary>
			/// Generate a keyboard button from text
			/// </summary>
			/// <param name="text">Button's text</param>
			/// <returns>Keyboard button</returns>
			public static implicit operator KeyboardButton(string text)
				=> new(text);


			/// <summary>
			/// Generate a keyboard button to request users
			/// </summary>
			/// <param name="text">Button's text</param>
			/// <param name="requestId">
			/// Signed 32-bit identifier of the request that will be received back in the <see cref="UsersShared"/> object.
			/// Must be unique within the message
			/// </param>
			public static KeyboardButton WithRequestUsers(string text, int requestId) =>
				new(text) { RequestUsers = new(requestId) };

			/// <summary>
			/// Creates a keyboard button. Pressing the button will open a list of suitable chats. Tapping on a chat will send its identifier to the bot in a <see cref="ChatShared"/> service message. Available in private chats only.
			/// </summary>
			/// <param name="text">Button's text</param>
			/// <param name="requestId">
			/// Signed 32-bit identifier of the request, which will be received back in the <see cref="ChatShared"/> object.
			/// Must be unique within the message
			/// </param>
			/// <param name="chatIsChannel">
			/// Pass <see langword="true"/> to request a channel chat, pass <see langword="false"/> to request a group or a supergroup chat.
			/// </param>
			public static KeyboardButton WithRequestChat(string text, int requestId, bool chatIsChannel) =>
				new(text) { RequestChat = new(requestId, chatIsChannel) };

			/// <summary>
			/// Generate a keyboard button to request a poll
			/// </summary>
			/// <param name="text">Button's text</param>
			/// <returns>Keyboard button</returns>
			public static KeyboardButton WithRequestPoll(string text) =>
				new(text) { RequestPoll = new() };
		}

		public partial class KeyboardButtonPollType
		{
			/// <summary>implicit from string</summary>
			public static implicit operator KeyboardButtonPollType(string type) => new() { Type = type };
		}
	}

	namespace InlineQueryResults
	{
		public partial class InputTextMessageContent
		{
			/// <summary>
			/// Disables link previews for links in this message
			/// </summary>
			[Obsolete($"This property is deprecated, use {nameof(LinkPreviewOptions)} instead")]
			public bool DisableWebPagePreview
			{
				get => LinkPreviewOptions?.IsDisabled ?? false;
				set
				{
					LinkPreviewOptions ??= new();
					LinkPreviewOptions.IsDisabled = value;
				}
			}
		}
	}
}
