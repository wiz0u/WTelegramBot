using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;
using TL;
using File = Telegram.Bot.Types.File;

#pragma warning disable CS1572, CS1580

namespace WTelegram;

public partial class Bot
{
	const int Reactions_uniq_max = 11;

	#region Power-up methods
	/// <summary>Use this method to get a list of members in a chat (can be incomplete).</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <param name="limit">The maximum number of member to fetch (big number might be slow to fetch, and Telegram might still restrict the maximum anyway)</param>
	/// <returns>On success, returns an Array of <see cref="ChatMember"/> objects that contains information about chat members</returns>
	/// <remarks>⚠️ For big chats, Telegram will likely limit the total number of members you can obtain with this method</remarks>
	public async Task<ChatMember[]> GetChatMemberList(ChatId chatId, int limit = 1000)
	{
		InputPeer chat = await InputPeerChat(chatId);
		if (chat is InputPeerChannel ipc)
		{
			var participants = new List<ChannelParticipantBase>();
			InputChannelBase channel = ipc;
			for (int offset = 0; ;)
			{
				var ccp = await Client.Channels_GetParticipants(channel, null, offset, limit - offset, 0);
				ccp.UserOrChat(_collector);
				participants.AddRange(ccp.participants);
				offset += ccp.participants.Length;
				if (offset >= ccp.count || offset >= limit || ccp.participants.Length == 0) break;
			}
			return await participants.Select(async p => p.ChatMember(await UserOrResolve(p.UserId))).WhenAllSequential();
		}
		else
		{
			var full = await Client.Messages_GetFullChat(chat.ID);
			full.UserOrChat(_collector);
			if (full.full_chat is not ChatFull { participants: ChatParticipants participants })
				throw new WTException($"Cannot fetch participants for chat {chatId}");
			return await participants.participants.Select(async p => p.ChatMember(await UserOrResolve(p.UserId))).WhenAllSequential();
		}
	}

	/// <summary>Get chat messages based on their messageIds</summary>
	/// <param name="chatId">The chat id or username</param>
	/// <param name="messageIds">The message IDs to fetch. You can use <c>Enumerable.Range(startMsgId, count)</c> to get a range of messages</param>
	/// <returns>List of messages that could be fetched</returns>
	public async Task<List<Message>> GetMessagesById(ChatId chatId, IEnumerable<int> messageIds)
	{
		var peer = await InputPeerChat(chatId);
		var msgs = await Client.GetMessages(peer, messageIds.Select(id => (InputMessageID)id).ToArray());
		msgs.UserOrChat(_collector);
		var messages = new List<Message>();
		foreach (var msgBase in msgs.Messages)
			if (await MakeMessage(msgBase) is { } msg)
				messages.Add(msg);
		return messages;
	}
	#endregion Power-up methods

	#region Available methods

	/// <summary>A simple method for testing your bot’s auth token.</summary>
	/// <returns>Returns basic information about the bot in form of a <see cref="User"/> object.</returns>
	public async Task<User> GetMe() =>
		(await _initTask).User();

	/// <summary>Use this method to close the bot instance before moving it from one local server to another. You need to
	/// delete the webhook before calling this method to ensure that the bot isn't launched again after server
	/// restart. The method will return error 429 in the first 10 minutes after the bot is launched.</summary>
	public async Task Close()
	{
		await Client.Auth_LogOut();
	}

	/// <summary>Use this method to send text messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="text">Text of the message to be sent, 1-4096 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="linkPreviewOptions">Link preview generation options for the message</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="entities">List of special entities that appear in message text, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendTextMessage(ChatId chatId, string text, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, LinkPreviewOptions? linkPreviewOptions = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? entities = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		ApplyParse(parseMode, ref text!, ref entities);
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = linkPreviewOptions.InputMediaWebPage();
		if (media == null)
			return await PostedMsg(Messages_SendMessage(businessConnectionId, peer, text, Helpers.RandomLong(), reply_to,
				await MakeReplyMarkup(replyMarkup), entities?.ToArray(), disableNotification, protectContent, messageEffectId,
				invert_media: linkPreviewOptions?.ShowAboveText == true, no_webpage: linkPreviewOptions?.IsDisabled == true),
				peer, text, replyToMessage);
		else
			return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, text, Helpers.RandomLong(), reply_to,
				await MakeReplyMarkup(replyMarkup), entities?.ToArray(), disableNotification, protectContent, messageEffectId, linkPreviewOptions?.ShowAboveText == true),
				peer, text, replyToMessage);
	}

	/// <summary>Use this method to forward messages of any kind. Service messages can't be forwarded.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="fromChatId">Unique identifier for the chat where the original message was sent
	/// (or channel username in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Message identifier in the chat specified in <paramref name="fromChatId"/></param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> ForwardMessage(ChatId chatId, ChatId fromChatId, int messageId, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default)
	{
		var peer = await InputPeerChat(chatId);
		return await PostedMsg(Client.Messages_ForwardMessages(await InputPeerChat(fromChatId), [messageId], [Helpers.RandomLong()], peer,
			top_msg_id: messageThreadId, silent: disableNotification, noforwards: protectContent), peer);
	}

	/// <summary>Use this method to forward multiple messages of any kind. If some of the specified messages can't be found
	/// or forwarded, they are skipped. Service messages and messages with protected content can't be forwarded.
	/// Album grouping is kept for forwarded messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="fromChatId">Unique identifier for the chat where the original messages were sent
	/// (or channel username in the format <c>@channelusername</c>)</param>
	/// <param name="messageIds">Identifiers of 1-100 messages in the chat from_chat_id to forward.
	/// The identifiers must be specified in a strictly increasing order.</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <returns>On success, an array of <see cref="MessageId"/> of the sent messages is returned.</returns>
	public async Task<MessageId[]> ForwardMessages(ChatId chatId, ChatId fromChatId, IEnumerable<int> messageIds, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default)
	{
		var peer = await InputPeerChat(chatId);
		var random_id = Helpers.RandomLong();
		var ids = messageIds.ToArray();
		var random_ids = new long[ids.Length];
		for (int i = 0; i < ids.Length; i++) random_ids[i] = random_id + i;
		var msgs = await PostedMsgs(Client.Messages_ForwardMessages(await InputPeerChat(fromChatId), ids, random_ids, peer,
			top_msg_id: messageThreadId, silent: disableNotification, noforwards: protectContent),
			ids.Length, random_id, null);
		return msgs.Select(m => (MessageId)m).ToArray();
	}

	/// <summary>Use this method to copy messages of any kind. Service messages and invoice messages can't be copied.
	/// The method is analogous to the method
	/// <see cref="ForwardMessage(ForwardMessageRequest)"/>,
	/// but the copied message doesn't have a link to the original message.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="fromChatId">Unique identifier for the chat where the original message was sent
	/// (or channel username in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Message identifier in the chat specified in <paramref name="fromChatId"/></param>
	/// <param name="caption">New caption for media, 0-1024 characters after entities parsing. If not specified, the original caption is kept</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media. Ignored if a new caption isn't specified.</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <returns>Returns the <see cref="MessageId"/> of the sent message on success.</returns>
	public async Task<MessageId> CopyMessage(ChatId chatId, ChatId fromChatId, int messageId,
		string? caption = default, ParseMode parseMode = default, ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default,
		bool disableNotification = default, bool protectContent = default)
	{
		var msgs = await Client.GetMessages(await InputPeerChat(fromChatId), messageId);
		msgs.UserOrChat(_collector);
		if (msgs.Messages.FirstOrDefault() is not TL.Message msg) throw new WTException("Bad Request: message to copy not found");
		ApplyParse(parseMode, ref caption, ref captionEntities);
		var peer = await InputPeerChat(chatId);
		var text = caption ?? msg.message;
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var task = msg.media == null
			? Messages_SendMessage(null, peer, text, Helpers.RandomLong(), reply_to,
				await MakeReplyMarkup(replyMarkup) ?? msg.reply_markup, caption != null ? captionEntities?.ToArray() : msg.entities,
				disableNotification, protectContent, 0, showCaptionAboveMedia, no_webpage: true)
			: Messages_SendMedia(null, peer, msg.media.ToInputMedia(), text, Helpers.RandomLong(), reply_to,
				await MakeReplyMarkup(replyMarkup) ?? msg.reply_markup, caption != null ? captionEntities?.ToArray() : msg.entities,
				disableNotification, protectContent, 0, showCaptionAboveMedia);
		var postedMsg = await PostedMsg(task, peer, text);
		return postedMsg;
	}

	/// <summary>Use this method to copy messages of any kind. If some of the specified messages can't be found or copied,
	/// they are skipped. Service messages, giveaway messages, giveaway winners messages, and invoice messages
	/// can't be copied. A quiz <see cref="Poll"/> can be copied only if the value of the field
	/// <see cref="Poll.CorrectOptionId">CorrectOptionId</see> is known to the bot. The method is analogous to the method
	/// <see cref="ForwardMessages(ForwardMessagesRequest)"/>, but the
	/// copied messages don't have a link to the original message. Album grouping is kept for copied messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="fromChatId">Unique identifier for the chat where the original messages were sent
	/// (or channel username in the format <c>@channelusername</c>)</param>
	/// <param name="messageIds">Identifiers of 1-100 messages in the chat <paramref name="fromChatId"/> to copy.
	/// The identifiers must be specified in a strictly increasing order.</param>
	/// <param name="removeCaption">Pass <see langword="true"/> to copy the messages without their captions</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <returns>On success, an array of <see cref="MessageId"/> of the sent messages is returned.</returns>
	public async Task<MessageId[]> CopyMessages(ChatId chatId, ChatId fromChatId, int[] messageIds, bool removeCaption = default,
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default)
	{
		var msgs = await Client.GetMessages(await InputPeerChat(fromChatId), messageIds.Select(id => (InputMessageID)id).ToArray());
		msgs.UserOrChat(_collector);
		var peer = await InputPeerChat(chatId);
		var reply_to = await MakeReplyTo(null, messageThreadId, peer);
		var msgIds = new List<MessageId>();
		long cur_grouped_id = 0;
		long start_random_id = Helpers.RandomLong(), random_id = start_random_id;
		bool grouped_invert_media = false;
		List<InputSingleMedia>? multiMedia = null;
		foreach (var msg in msgs.Messages.OfType<TL.Message>())
		{
			if (removeCaption && msg.media is not null and not MessageMediaWebPage) { msg.message = null; msg.entities = null; }
			if (msg.grouped_id != 0)
			{
				if (msg.grouped_id != cur_grouped_id && multiMedia != null) await FlushMediaGroup();
				grouped_invert_media |= msg.flags.HasFlag(TL.Message.Flags.invert_media);
				(multiMedia ??= []).Add(new InputSingleMedia
				{
					media = msg.media?.ToInputMedia(),
					random_id = random_id++,
					message = msg.message,
					entities = msg.entities,
					flags = msg.entities != null ? TL.InputSingleMedia.Flags.has_entities : 0
				});
				cur_grouped_id = msg.grouped_id;
				continue;
			}
			if (multiMedia != null) await FlushMediaGroup();
			cur_grouped_id = 0;
			var task = msg.media == null
				? Messages_SendMessage(null, peer, msg.message, random_id++, reply_to, 
					null, msg.entities, disableNotification, protectContent, 0, no_webpage: true)
				: Messages_SendMedia(null, peer, msg.media.ToInputMedia(), msg.message, random_id++, reply_to,
					null, msg.entities, disableNotification, protectContent, 0, msg.flags.HasFlag(TL.Message.Flags.invert_media));
			var postedMsg = await PostedMsg(task, peer);
			msgIds.Add(postedMsg);
		}
		if (multiMedia != null) await FlushMediaGroup();
		return [.. msgIds];

		async Task FlushMediaGroup()
		{
			var postedMsgs = await PostedMsgs(Client.Messages_SendMultiMedia(peer, multiMedia?.ToArray(), reply_to,
				silent: disableNotification, noforwards: protectContent, invert_media: grouped_invert_media),
				multiMedia!.Count, multiMedia[0].random_id, null);
			msgIds.AddRange(postedMsgs.Select(m => (MessageId)m));
			multiMedia = null;
			grouped_invert_media = false;
		}
	}

	/// <summary>Use this method to send photos.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="photo">Photo to send. Pass a <see cref="InputFileId"/> as String to send a photo that exists on
	/// the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a photo from
	/// the Internet, or upload a new photo using multipart/form-data. The photo must be at most 10 MB in size.
	/// The photo's width and height must not exceed 10000 in total. Width and height ratio must be at most 20</param>
	/// <param name="caption">Photo caption (may also be used when resending photos by <see cref="InputFileId"/>),
	/// 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media. Ignored if a new caption isn't specified.</param>
	/// <param name="hasSpoiler">Pass <see langword="true"/> if the photo needs to be covered with a spoiler animation</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendPhoto(ChatId chatId, InputFile photo, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, bool hasSpoiler = default, 
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		ApplyParse(parseMode, ref caption, ref captionEntities);
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = await InputMediaPhoto(photo, hasSpoiler);
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, caption, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), captionEntities?.ToArray(), disableNotification, protectContent, messageEffectId, showCaptionAboveMedia),
			peer, caption, replyToMessage);
	}

	private async Task<Message> SendDocument(ChatId chatId, InputFile file, string? caption, ParseMode parseMode,
		ReplyParameters? replyParameters, IReplyMarkup? replyMarkup, InputFile? thumbnail,
		int messageThreadId, IEnumerable<MessageEntity>? captionEntities, bool disableNotification,
		bool protectContent, long messageEffectId, bool showCaptionAboveMedia, string? businessConnectionId,
		string? defaultFilename, bool hasSpoiler, Action<InputMediaUploadedDocument>? prepareDoc)
	{
		ApplyParse(parseMode, ref caption, ref captionEntities);
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = await InputMediaDocument(file, hasSpoiler, defaultFilename: defaultFilename);
		if (media is TL.InputMediaUploadedDocument doc)
		{
			prepareDoc?.Invoke(doc);
			await SetDocThumb(doc, thumbnail);
		}
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, caption, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), captionEntities?.ToArray(), disableNotification, protectContent, messageEffectId, showCaptionAboveMedia),
			peer, caption, replyToMessage);
	}

	/// <summary>Use this method to send audio files, if you want Telegram clients to display them in the music player.
	/// Your audio must be in the .MP3 or .M4A format. Bots can currently send audio files of up to 50 MB in size,
	/// this limit may be changed in the future.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="audio">Audio file to send. Pass a <see cref="InputFileId"/> as String to send an audio file that
	/// exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get an audio
	/// file from the Internet, or upload a new one using multipart/form-data</param>
	/// <param name="caption">Audio caption, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="duration">Duration of the audio in seconds</param>
	/// <param name="performer">Performer</param>
	/// <param name="title">Track name</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side.
	/// The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height
	/// should not exceed 320. Ignored if the file is not uploaded using multipart/form-data. Thumbnails can't be
	/// reused and can be only uploaded as a new file, so you can pass "attach://&lt;file_attach_name&gt;" if the
	/// thumbnail was uploaded using multipart/form-data under &lt;file_attach_name&gt;</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendAudio(ChatId chatId, InputFile audio, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default,
		int duration = 0, string? performer = default, string? title = default, InputFile? thumbnail = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, 
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		return await SendDocument(chatId, audio, caption, parseMode, replyParameters, replyMarkup, thumbnail, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, false, businessConnectionId, default, default, doc =>
			doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeAudio {
				duration = duration, performer = performer, title = title,
				flags = DocumentAttributeAudio.Flags.has_title | DocumentAttributeAudio.Flags.has_performer }]);
	}

	/// <summary>Use this method to send general files. Bots can currently send files of any type of up to 50 MB in size,
	/// this limit may be changed in the future.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="document">File to send. Pass a <see cref="InputFileId"/> as String to send a file that exists on the
	/// Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a file from the Internet,
	/// or upload a new one using multipart/form-data</param>
	/// <param name="caption">Document caption (may also be used when resending documents by file_id), 0-1024 characters after
	/// entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side.
	/// The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should
	/// not exceed 320. Ignored if the file is not uploaded using multipart/form-data. Thumbnails can't be reused
	/// and can be only uploaded as a new file, so you can pass "attach://&lt;file_attach_name&gt;" if the
	/// thumbnail was uploaded using multipart/form-data under &lt;file_attach_name&gt;</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="disableContentTypeDetection">Disables automatic server-side content type detection for files uploaded using multipart/form-data</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendDocument(ChatId chatId, InputFile document, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, InputFile? thumbnail = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, bool disableContentTypeDetection = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		return await SendDocument(chatId, document, caption, parseMode, replyParameters, replyMarkup, thumbnail, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, false, businessConnectionId, "document", default, doc =>
			{ if (disableContentTypeDetection) doc.flags |= InputMediaUploadedDocument.Flags.force_file; });
	}

	/// <summary>Use this method to send video files, Telegram clients support mp4 videos (other formats may be sent as
	/// <see cref="Document"/>). Bots can currently send video files of up to 50 MB in size, this limit may be
	/// changed in the future.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="video">Video to send. Pass a <see cref="InputFileId"/> as String to send a video that exists on
	/// the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a video from the
	/// Internet, or upload a new video using multipart/form-data</param>
	/// <param name="caption">Video caption (may also be used when resending videos by file_id), 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="duration">Duration of sent video in seconds</param>
	/// <param name="width">Video width</param>
	/// <param name="height">Video height</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side.
	/// The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should
	/// not exceed 320. Ignored if the file is not uploaded using multipart/form-data. Thumbnails can't be reused
	/// and can be only uploaded as a new file, so you can pass "attach://&lt;file_attach_name&gt;" if the
	/// thumbnail was uploaded using multipart/form-data under &lt;file_attach_name&gt;</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media. Ignored if a new caption isn't specified.</param>
	/// <param name="hasSpoiler">Pass <see langword="true"/> if the video needs to be covered with a spoiler animation</param>
	/// <param name="supportsStreaming">Pass <see langword="true"/>, if the uploaded video is suitable for streaming</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendVideo(ChatId chatId, InputFile video, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default,
		int duration = 0, int width = 0, int height = 0, InputFile? thumbnail = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, bool hasSpoiler = default, bool supportsStreaming = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		return await SendDocument(chatId, video, caption, parseMode, replyParameters, replyMarkup, thumbnail, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, showCaptionAboveMedia, businessConnectionId, default, hasSpoiler, doc =>
			doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeVideo {
				duration = duration, h = height, w = width,
				flags = supportsStreaming ? DocumentAttributeVideo.Flags.supports_streaming : 0 }]);
	}

	/// <summary>Use this method to send animation files (GIF or H.264/MPEG-4 AVC video without sound). Bots can currently
	/// send animation files of up to 50 MB in size, this limit may be changed in the future.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="animation">Animation to send. Pass a <see cref="InputFileId"/> as String to send an animation that
	/// exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get an
	/// animation from the Internet, or upload a new animation using multipart/form-data</param>
	/// <param name="caption">Animation caption (may also be used when resending animation by <see cref="InputFileId"/>),
	/// 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="duration">Duration of sent animation in seconds</param>
	/// <param name="width">Animation width</param>
	/// <param name="height">Animation height</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side.
	/// The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should
	/// not exceed 320. Ignored if the file is not uploaded using multipart/form-data. Thumbnails can't be reused
	/// and can be only uploaded as a new file, so you can pass "attach://&lt;file_attach_name&gt;" if the
	/// thumbnail was uploaded using multipart/form-data under &lt;file_attach_name&gt;</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media</param>
	/// <param name="hasSpoiler">Pass <see langword="true"/> if the animation needs to be covered with a spoiler animation</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendAnimation(ChatId chatId, InputFile animation, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default,
		int duration = 0, int width = 0, int height = 0, InputFile? thumbnail = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, bool hasSpoiler = default, 
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		return await SendDocument(chatId, animation, caption, parseMode, replyParameters, replyMarkup, thumbnail, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, showCaptionAboveMedia, businessConnectionId, "animation", hasSpoiler, doc =>
			{
				doc.attributes ??= [];
				if (doc.mime_type == "video/mp4")
					doc.attributes = [.. doc.attributes, new DocumentAttributeVideo { duration = duration, w = width, h = height }];
				else if (width > 0 && height > 0)
				{
					if (doc.mime_type?.StartsWith("image/") != true) doc.mime_type = "image/gif";
					doc.attributes = [.. doc.attributes, new DocumentAttributeImageSize { w = width, h = height }];
				}
			});
	}

	/// <summary>Use this method to send audio files, if you want Telegram clients to display the file as a playable voice
	/// message. For this to work, your audio must be in an .OGG file encoded with OPUS (other formats may be sent
	/// as <see cref="Audio"/> or <see cref="Document"/>). Bots can currently send voice messages of up to 50 MB
	/// in size, this limit may be changed in the future.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="voice">Audio file to send. Pass a <see cref="InputFileId"/> as String to send a file that exists
	/// on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a file from
	/// the Internet, or upload a new one using multipart/form-data</param>
	/// <param name="caption">Voice message caption, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="duration">Duration of the voice message in seconds</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendVoice(ChatId chatId, InputFile voice, string? caption = default, ParseMode parseMode = default, 
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, int duration = 0, 
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, 
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		return await SendDocument(chatId, voice, caption, parseMode, replyParameters, replyMarkup, null, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, false, businessConnectionId, default, default, doc =>
			{
				doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeAudio {
					duration = duration, flags = DocumentAttributeAudio.Flags.voice }];
				if (doc.mime_type is not "audio/ogg" and not "audio/mpeg" and not "audio/mp4") doc.mime_type = "audio/ogg";
			});
	}

	/// <summary>As of <a href="https://telegram.org/blog/video-messages-and-telescope">v.4.0</a>, Telegram clients
	/// support rounded square mp4 videos of up to 1 minute long. Use this method to send video messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="videoNote">Video note to send. Pass a <see cref="InputFileId"/> as String to send a video note that
	/// exists on the Telegram servers (recommended) or upload a new video using multipart/form-data. Sending
	/// video notes by a URL is currently unsupported</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="duration">Duration of sent video in seconds</param>
	/// <param name="length">Video width and height, i.e. diameter of the video message</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side.
	/// The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should
	/// not exceed 320. Ignored if the file is not uploaded using multipart/form-data. Thumbnails can't be reused
	/// and can be only uploaded as a new file, so you can pass "attach://&lt;file_attach_name&gt;" if the
	/// thumbnail was uploaded using multipart/form-data under &lt;file_attach_name&gt;</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendVideoNote(ChatId chatId, InputFile videoNote, 
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, 
		int duration = 0, int? length = default, InputFile? thumbnail = default, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		return await SendDocument(chatId, videoNote, default, default, replyParameters, replyMarkup, thumbnail, messageThreadId,
			default, disableNotification, protectContent, messageEffectId, false, businessConnectionId, default, default, doc =>
			{
				doc.flags |= InputMediaUploadedDocument.Flags.nosound_video;
				doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeVideo {
					flags = DocumentAttributeVideo.Flags.round_message, duration = duration, w = length ?? 384, h = length ?? 384 }];
			});
	}

	/// <summary>Use this method to send a group of photos, videos, documents or audios as an album. Documents and audio
	/// files can be only grouped in an album with messages of the same type.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="medias">An array describing messages to be sent, must include 2-10 items</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, an array of <see cref="Message"/>s that were sent is returned.</returns>
	public async Task<Message[]> SendMediaGroup(ChatId chatId, IEnumerable<IAlbumInputMedia> medias, ReplyParameters? replyParameters = default, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		List<InputSingleMedia> multimedia = [];
		var random_id = Helpers.RandomLong();
		bool invert_media = false;
		foreach (var aim in medias)
		{
			var media = (InputMedia)aim;
			invert_media |= media switch { Telegram.Bot.Types.InputMediaPhoto imp => imp.ShowCaptionAboveMedia, InputMediaVideo imv => imv.ShowCaptionAboveMedia, InputMediaAnimation ima => ima.ShowCaptionAboveMedia, _ => false };
			var ism = await InputSingleMedia(media);
			ism.random_id = random_id + multimedia.Count;
			if (media.Media.FileType != FileType.Id) // External or Uploaded
				ism.media = (await Client.Messages_UploadMedia(peer, ism.media)).ToInputMedia();
			multimedia.Add(ism);
		}
		return await PostedMsgs(Messages_SendMultiMedia(businessConnectionId, peer, [.. multimedia], reply_to,
			silent: disableNotification, noforwards: protectContent, invert_media: invert_media, effect: messageEffectId),
			multimedia.Count, random_id, replyToMessage);
	}

	/// <summary>Use this method to send point on the map.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="latitude">Latitude of location</param>
	/// <param name="longitude">Longitude of location</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="horizontalAccuracy">The radius of uncertainty for the location, measured in meters; 0-1500</param>
	/// <param name="livePeriod">Period in seconds for which the location will be updated, should be between 60 and 86400</param>
	/// <param name="heading">For live locations, a direction in which the user is moving, in degrees. Must be between 1 and 360 if specified</param>
	/// <param name="proximityAlertRadius">For live locations, a maximum distance for proximity alerts about approaching another chat member,
	/// in meters. Must be between 1 and 100000 if specified</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendLocation(ChatId chatId, double latitude, double longitude,
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, 
		int horizontalAccuracy = 0, int livePeriod = 0, int heading = 0, int proximityAlertRadius = 0, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		TL.InputMedia media = livePeriod > 0 ? MakeGeoLive(latitude, longitude, horizontalAccuracy, heading, proximityAlertRadius, livePeriod)
			: new TL.InputMediaGeoPoint { geo_point = new InputGeoPoint { lat = latitude, lon = longitude } };
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, disableNotification, protectContent, messageEffectId),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method to send information about a venue.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="latitude">Latitude of the venue</param>
	/// <param name="longitude">Longitude of the venue</param>
	/// <param name="title">Name of the venue</param>
	/// <param name="address">Address of the venue</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="foursquareId">Foursquare identifier of the venue</param>
	/// <param name="foursquareType">Foursquare type of the venue, if known. (For example, “arts_entertainment/default”,
	/// “arts_entertainment/aquarium” or “food/icecream”.)</param>
	/// <param name="googlePlaceId">Google Places identifier of the venue</param>
	/// <param name="googlePlaceType">Google Places type of the venue. (See
	/// <a href="https://developers.google.com/places/web-service/supported_types">supported types</a>)</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	/// <a href="https://core.telegram.org/bots/api#sendvenue"/>
	public async Task<Message> SendVenue(ChatId chatId, double latitude, double longitude, string title, string address,
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, 
		string? foursquareId = default, string? foursquareType = default, string? googlePlaceId = default, string? googlePlaceType = default,
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = new InputMediaVenue
		{
			geo_point = new InputGeoPoint { lat = latitude, lon = longitude },
			title = title,
			address = address,
		};
		if (googlePlaceId != null || googlePlaceType != null)
		{
			media.provider = "gplaces";
			media.venue_id = googlePlaceId;
			media.venue_type = googlePlaceType;
		}
		if (foursquareId != null || foursquareType != null)
		{
			media.provider = "foursquare";
			media.venue_id = foursquareId;
			media.venue_type = foursquareType;
		}
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, disableNotification, protectContent, messageEffectId),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method to send phone contacts.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="phoneNumber">Contact's phone number</param>
	/// <param name="firstName">Contact's first name</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="lastName">Contact's last name</param>
	/// <param name="vcard">Additional data about the contact in the form of a vCard, 0-2048 bytes</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the action will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendContact(ChatId chatId, string phoneNumber, string firstName, string? lastName = default, string? vcard = default, 
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = new InputMediaContact
		{
			phone_number = phoneNumber,
			first_name = firstName,
			last_name = lastName,
			vcard = vcard
		};
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, disableNotification, protectContent, messageEffectId),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method to send a native poll.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="question">Poll question, 1-300 characters</param>
	/// <param name="questionParseMode">Mode for parsing entities in the question. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details</param>
	/// <param name="questionEntities">List of special entities that appear in the poll explanation, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="options">A list of answer options, 2-10 strings 1-100 characters each</param>
	/// <param name="isAnonymous"><see langword="true"/>, if the poll needs to be anonymous, defaults to <see langword="true"/></param>
	/// <param name="type">Poll type, <see cref="PollType.Quiz"/> or <see cref="PollType.Regular"/></param>
	/// <param name="allowsMultipleAnswers"><see langword="true"/>, if the poll allows multiple answers, ignored for polls in quiz mode</param>
	/// <param name="correctOptionId">0-based identifier of the correct answer option, required for polls in quiz mode</param>
	/// <param name="explanation">Text that is shown when a user chooses an incorrect answer or taps on the lamp icon in a quiz-style poll,
	/// 0-200 characters with at most 2 line feeds after entities parsing</param>
	/// <param name="explanationParseMode">Mode for parsing entities in the explanation. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details</param>
	/// <param name="explanationEntities">List of special entities that appear in the poll explanation, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="openPeriod">Amount of time in seconds the poll will be active after creation, 5-600. Can't be used together
	/// with <paramref name="closeDate"/></param>
	/// <param name="closeDate">Point in time when the poll will be automatically closed. Must be at least 5 and no more than 600 seconds
	/// in the future. Can't be used together with <paramref name="openPeriod"/></param>
	/// <param name="isClosed">Pass <see langword="true"/>, if the poll needs to be immediately closed. This can be useful for poll preview</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the action will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendPoll(ChatId chatId, string question, IEnumerable<InputPollOption> options,
		bool isAnonymous = true, PollType type = PollType.Regular, bool allowsMultipleAnswers = default, int? correctOptionId = default, 
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, 
		string? explanation = default, ParseMode explanationParseMode = default, IEnumerable<MessageEntity>? explanationEntities = default,
		ParseMode questionParseMode = default, IEnumerable<MessageEntity>? questionEntities = default,
		int? openPeriod = default, DateTime? closeDate = default, bool isClosed = default,
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		ApplyParse(explanationParseMode, ref explanation, ref explanationEntities);
		ApplyParse(questionParseMode, ref question!, ref questionEntities);
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = new InputMediaPoll
		{
			poll = new TL.Poll
			{
				flags = (isClosed ? TL.Poll.Flags.closed : 0)
					| (isAnonymous == false ? TL.Poll.Flags.public_voters : 0)
					| (allowsMultipleAnswers ? TL.Poll.Flags.multiple_choice : 0)
					| (type == PollType.Quiz ? TL.Poll.Flags.quiz : 0)
					| (openPeriod.HasValue ? TL.Poll.Flags.has_close_period : 0)
					| (closeDate.HasValue ? TL.Poll.Flags.has_close_date : 0),
				question = new() { text = question, entities = questionEntities?.ToArray() },
				answers = options.Select(MakePollAnswer).ToArray(),
				close_period = openPeriod.GetValueOrDefault(),
				close_date = closeDate.GetValueOrDefault(),
			},
			correct_answers = correctOptionId == null ? null : [[(byte)correctOptionId]],
			solution = explanation,
			solution_entities = explanationEntities?.ToArray(),
			flags = (explanation != null ? InputMediaPoll.Flags.has_solution : 0)
				| (correctOptionId >= 0 ? InputMediaPoll.Flags.has_correct_answers : 0)
		};
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, disableNotification, protectContent, messageEffectId),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method to send an animated emoji that will display a random value.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="emoji">Emoji on which the dice throw animation is based. Currently, must be one of <see cref="Emoji.Dice"/>,
	/// <see cref="Emoji.Darts"/>, <see cref="Emoji.Basketball"/>, <see cref="Emoji.Football"/>,
	/// <see cref="Emoji.Bowling"/> or <see cref="Emoji.SlotMachine"/>. Dice can have values 1-6 for
	/// <see cref="Emoji.Dice"/>, <see cref="Emoji.Darts"/> and <see cref="Emoji.Bowling"/>, values 1-5 for
	/// <see cref="Emoji.Basketball"/> and <see cref="Emoji.Football"/>, and values 1-64 for
	/// <see cref="Emoji.SlotMachine"/>.</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the action will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendDice(ChatId chatId, Emoji emoji = Emoji.Dice, 
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = new InputMediaDice { emoticon = emoji.GetDisplayName() };
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, disableNotification, protectContent, messageEffectId),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method when you need to tell the user that something is happening on the bot’s side. The status is
	/// set for 5 seconds or less (when a message arrives from your bot, Telegram clients clear its typing status).</summary>
	/// <example><para>
	/// The <a href="https://t.me/imagebot">ImageBot</a> needs some time to process a request and upload the
	/// image. Instead of sending a text message along the lines of “Retrieving image, please wait…”, the bot may
	/// use <see cref="SendChatAction(SendChatActionRequest)"/> with
	/// <see cref="SendChatActionRequest.Action"/> = <see cref="ChatAction.UploadPhoto"/>.
	/// The user will see a “sending photo” status for the bot.</para>
	/// <para>We only recommend using this method when a response from the bot will take a <b>noticeable</b> amount of time to arrive.</para></example>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="action">Type of action to broadcast. Choose one, depending on what the user is about to receive:
	/// <see cref="ChatAction.Typing"/> for <see cref="SendTextMessageAsync">text messages</see>,
	/// <see cref="ChatAction.UploadPhoto"/> for
	/// <see cref="SendPhoto(SendPhotoRequest)">photos</see>,
	/// <see cref="ChatAction.RecordVideo"/> or <see cref="ChatAction.UploadVideo"/> for
	/// <see cref="SendVideo(SendVideoRequest)">videos</see>,
	/// <see cref="ChatAction.RecordVoice"/> or <see cref="ChatAction.UploadVoice"/> for
	/// <see cref="SendVoice(SendVoiceRequest)">voice notes</see>,
	/// <see cref="ChatAction.UploadDocument"/> for
	/// <see cref="SendDocument(SendDocumentRequest)">general files</see>,
	/// <see cref="ChatAction.FindLocation"/> for
	/// <see cref="SendLocation(SendLocationRequest)">location data</see>,
	/// <see cref="ChatAction.RecordVideoNote"/> or <see cref="ChatAction.UploadVideoNote"/> for
	/// <see cref="SendVideoNote(SendVideoNoteRequest)">video notes</see></param>
	/// <param name="messageThreadId">Unique identifier for the target message thread; supergroups only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the action will be sent</param>
	public async Task SendChatAction(ChatId chatId, ChatAction action, int messageThreadId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		if (businessConnectionId is null)
			await Client.Messages_SetTyping(peer, action.ChatAction(), messageThreadId);
		else
			await Client.InvokeWithBusinessConnection(businessConnectionId,
				new TL.Methods.Messages_SetTyping
				{
					flags = (TL.Methods.Messages_SetTyping.Flags)(messageThreadId != 0 ? 0x1 : 0),
					peer = peer,
					top_msg_id = messageThreadId,
					action = action.ChatAction(),
				});
	}

	/// <summary>Use this method to change the chosen reactions on a message. Service messages can't be reacted to.
	/// Automatically forwarded messages from a channel to its discussion group have the same
	/// available reactions as messages in the channel.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the target message. If the message belongs to a media group, the reaction
	/// is set to the first non-deleted message in the group instead.</param>
	/// <param name="reaction">New list of reaction types to set on the message. Currently, as non-premium users, bots can
	/// set up to one reaction per message. A custom emoji reaction can be used if it is either
	/// already present on the message or explicitly allowed by chat administrators.</param>
	/// <param name="isBig">Pass <see langword="true"/> to set the reaction with a big animation</param>
	public async Task SetMessageReaction(ChatId chatId, int messageId, IEnumerable<ReactionType>? reaction, bool isBig = default)
	{
		var peer = await InputPeerChat(chatId);
		reaction ??= [];
		var updates = await Client.Messages_SendReaction(peer, messageId, reaction.Select(TypesTLConverters.Reaction).ToArray(), big: isBig);
	}

	/// <summary>Use this method to get a list of profile pictures for a user.</summary>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="offset">Sequential number of the first photo to be returned. By default, all photos are returned</param>
	/// <param name="limit">Limits the number of photos to be retrieved. Values between 1-100 are accepted.</param>
	/// <returns>Returns a <see cref="UserProfilePhotos"/> object</returns>
	public async Task<UserProfilePhotos> GetUserProfilePhotos(long userId, int offset = 0, int limit = 100)
	{
		var inputUser = InputUser(userId);
		var photos = await Client.Photos_GetUserPhotos(inputUser, offset, limit: limit);
		return new UserProfilePhotos
		{
			TotalCount = (photos as Photos_PhotosSlice)?.count ?? photos.photos.Length,
			Photos = photos.photos.Select(pb => pb.PhotoSizes()!).ToArray()
		};
	}

	/// <summary>Use this method to get basic info about a file and prepare it for downloading. For the moment, bots can
	/// download files of up to 20MB in size. The file can then be downloaded via the link
	/// <c>https://api.telegram.org/file/bot&lt;token&gt;/&lt;file_path&gt;</c>, where <c>&lt;file_path&gt;</c>
	/// is taken from the response. It is guaranteed that the link will be valid for at least 1 hour.
	/// When the link expires, a new one can be requested by calling
	/// <see cref="GetFile(GetFileRequest)"/> again.</summary>
	/// <remarks>You can use <see cref="DownloadFileAsync"/> or
	/// <see cref="TelegramBotClientExtensions.GetInfoAndDownloadFileAsync"/> methods to download the file</remarks>
	/// <param name="fileId">File identifier to get info about</param>
	/// <returns>On success, a <see cref="File"/> object is returned.</returns>
	public Task<File> GetFile(string fileId) =>
		Task.FromResult(fileId.ParseFileId(true).file);

	/// <summary>Use this method to download a file.</summary>
	/// <param name="fileIdOrPath">File identifier or file path obtained from <see cref="File.FilePath"/></param>
	/// <param name="destination">Destination stream to write file to</param>
	/// <param name="cancellationToken">If you need to abort the download</param>
	public async Task DownloadFile(string fileIdOrPath, Stream destination, CancellationToken cancellationToken = default)
	{
		int slash = fileIdOrPath.IndexOf('/');
		await GetInfoAndDownloadFile(slash < 0 ? fileIdOrPath : fileIdOrPath[..slash], destination, cancellationToken);
	}

	/// <summary>Use this method to get basic info about a file download it. For the moment, bots can download files
	/// of up to 20MB in size.</summary>
	/// <param name="fileId">File identifier to get info about</param>
	/// <param name="destination">Destination stream to write file to</param>
	/// <param name="cancellationToken">If you need to abort the download</param>
	/// <returns>On success, a <see cref="File"/> object is returned.</returns>
	public async Task<File> GetInfoAndDownloadFile(string fileId, Stream destination, CancellationToken cancellationToken = default)
	{
		var (file, location, dc_id) = fileId.ParseFileId(true);
		await Client.DownloadFileAsync(location, destination, dc_id, file.FileSize ?? 0, (t, s) => cancellationToken.ThrowIfCancellationRequested());
		return file;
	}

	/// <summary>Use this method to ban a user in a group, a supergroup or a channel. In the case of supergroups and
	/// channels, the user will not be able to return to the chat on their own using invite links, etc., unless
	/// <see cref="UnbanChatMember( ChatId, long, bool)">unbanned</see>
	/// first. The bot must be an administrator in the chat for this to work and must have the appropriate admin rights.</summary>
	/// <param name="chatId">Unique identifier for the target group or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="untilDate">Date when the user will be unbanned. If user is banned for more than 366 days or less than 30 seconds
	/// from the current time they are considered to be banned forever. Applied for supergroups and channels only</param>
	/// <param name="revokeMessages">Pass <see langword="true"/> to delete all messages from the chat for the user that is being removed.
	/// If <see langword="false"/>, the user will be able to see messages in the group that were sent before the user was
	/// removed. Always <see langword="true"/> for supergroups and channels</param>
	public async Task BanChatMember(ChatId chatId, long userId, DateTime untilDate = default, bool revokeMessages = default)
	{
		var peer = await InputPeerChat(chatId);
		var user = InputPeerUser(userId);
		switch (peer)
		{
			case InputPeerChat chat:
				await Client.Messages_DeleteChatUser(chat.chat_id, user, revokeMessages);
				break;
			case InputPeerChannel channel:
				await Client.Channels_EditBanned(channel, user,
					new ChatBannedRights { flags = ChatBannedRights.Flags.view_messages, until_date = untilDate });
				break;
			default: throw new WTException("Bad Request: can't ban members in private chats");
		}
	}

	/// <summary>Use this method to unban a previously banned user in a supergroup or channel. The user will <b>not</b>
	/// return to the group or channel automatically, but will be able to join via link, etc. The bot must be an
	/// administrator for this to work. By default, this method guarantees that after the call the user is not a
	/// member of the chat, but will be able to join it. So if the user is a member of the chat they will also be
	/// <b>removed</b> from the chat. If you don't want this, use the parameter <paramref name="onlyIfBanned"/></summary>
	/// <param name="chatId">Unique identifier for the target group or username of the target supergroup or channel (in the format <c>@username</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="onlyIfBanned">Do nothing if the user is not banned</param>
	public async Task UnbanChatMember(ChatId chatId, long userId, bool onlyIfBanned = default)
	{
		var channel = await InputChannel(chatId);
		var user = InputPeerUser(userId);
		if (onlyIfBanned)
		{
			try
			{
				var part = await Client.Channels_GetParticipant(channel, user);
				part.UserOrChat(_collector);
				if (part.participant is not ChannelParticipantBanned) return;
			}
			catch (RpcException)
			{
				return;
			}
		}
		else // weird behaviour of Bot API: user is removed first before unbanned
			await Client.Channels_EditBanned(channel, user, new ChatBannedRights { flags = ChatBannedRights.Flags.view_messages });
		await Client.Channels_EditBanned(channel, user, new ChatBannedRights { });
	}

	/// <summary>Use this method to restrict a user in a supergroup. The bot must be an administrator in the supergroup
	/// for this to work and must have the appropriate admin rights. Pass <see langword="true"/> for all permissions to
	/// lift restrictions from a user.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="permissions">New user permissions</param>
	/// <param name="untilDate">Date when restrictions will be lifted for this user; Unix time. If 0, then the user is restricted forever</param>
	public async Task RestrictChatMember(ChatId chatId, long userId, ChatPermissions permissions, DateTime? untilDate = default)
	{
		var channel = await InputChannel(chatId);
		var user = InputPeerUser(userId);
		await Client.Channels_EditBanned(channel, user, permissions.ToChatBannedRights(untilDate));
	}

	/// <summary>Use this method to promote or demote a user in a supergroup or a channel. The bot must be an administrator in
	/// the chat for this to work and must have the appropriate admin rights. Pass <c><see langword="null"/></c> rights to demote a user.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="rights">An object describing new administrator rights.</param>
	/// <param name="customTitle">Give an admin title to the user (exclusive!)</param>
	public async Task PromoteChatMember(ChatId chatId, long userId, ChatAdministratorRights? rights, string? customTitle = null)
	{
		var channel = await InputChannel(chatId);
		var user = InputPeerUser(userId);
		await Client.Channels_EditAdmin(channel, user, rights.ChatAdminRights(), customTitle);
	}

	/// <summary>Use this method to set a custom title for an administrator in a supergroup promoted by the bot.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="customTitle">New custom title for the administrator; 0-16 characters, emoji are not allowed</param>
	public async Task SetChatAdministratorCustomTitle(ChatId chatId, long userId, string customTitle)
	{
		var channel = await InputChannel(chatId);
		var user = InputPeerUser(userId);
		var part = await Client.Channels_GetParticipant(channel, user);
		part.UserOrChat(_collector);
		if (part.participant is not ChannelParticipantAdmin admin)
			throw new WTException("Bad Request: user is not an administrator");
		if (!admin.flags.HasFlag(ChannelParticipantAdmin.Flags.can_edit))
			throw new WTException("Bad Request: not enough rights to change custom title of the user");
		await Client.Channels_EditAdmin(channel, user, admin.admin_rights, customTitle);
	}

	/// <summary>Use this method to ban (or unban) a channel chat in a supergroup or a channel. The owner of the chat will not be
	/// able to send messages and join live streams on behalf of the chat, unless it is unbanned first. The bot
	/// must be an administrator in the supergroup or channel for this to work and must have the appropriate
	/// administrator rights. Returns <see langword="true"/> on success.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="senderChatId">Unique identifier of the target sender chat</param>
	/// <param name="ban">whether to ban or unban</param>
	public async Task BanUnbanChatSenderChat(ChatId chatId, long senderChatId, bool ban = true)
	{
		var channel = await InputChannel(chatId);
		var senderChat = await InputPeerChat(senderChatId);
		await Client.Channels_EditBanned(channel, senderChat, new ChatBannedRights { flags = ban ? ChatBannedRights.Flags.view_messages : 0 });
	}

	/// <summary>Use this method to set default chat permissions for all members. The bot must be an administrator
	/// in the group or a supergroup for this to work and must have the can_restrict_members admin rights</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="permissions">New default chat permissions</param>
	public async Task SetChatPermissions(ChatId chatId, ChatPermissions permissions)
	{
		var peer = await InputPeerChat(chatId);
		try
		{
		await Client.Messages_EditChatDefaultBannedRights(peer, permissions.ToChatBannedRights());
		}
		catch (RpcException ex) when (ex.Message.EndsWith("_NOT_MODIFIED")) { }
	}

	/// <summary>Use this method to generate a new primary invite link for a chat; any previously generated primary
	/// link is revoked. The bot must be an administrator in the chat for this to work and must have the appropriate admin rights</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	public async Task<string> ExportChatInviteLink(ChatId chatId)
	{
		var peer = await InputPeerChat(chatId);
		var exported = (ChatInviteExported)await Client.Messages_ExportChatInvite(peer, legacy_revoke_permanent: true);
		return exported.link;
	}

	/// <summary>Use this method to create an additional invite link for a chat. The bot must be an administrator
	/// in the chat for this to work and must have the appropriate admin rights. The link can be revoked
	/// using the method
	/// <see cref="RevokeChatInviteLink(RevokeChatInviteLinkRequest)"/></summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="name">Invite link name; 0-32 characters</param>
	/// <param name="expireDate">Point in time when the link will expire</param>
	/// <param name="memberLimit">Maximum number of users that can be members of the chat simultaneously after joining the chat
	/// via this invite link; 1-99999</param>
	/// <param name="createsJoinRequest">Set to <see langword="true"/>, if users joining the chat via the link need to be approved by chat administrators.
	/// If <see langword="true"/>, <paramref name="memberLimit"/> can't be specified</param>
	/// <returns>Returns the new invite link as <see cref="ChatInviteLink"/> object.</returns>
	public async Task<ChatInviteLink> CreateChatInviteLink(ChatId chatId, string? name = default, DateTime? expireDate = default, int? memberLimit = default, bool createsJoinRequest = default)
	{
		var peer = await InputPeerChat(chatId);
		ExportedChatInvite exported = await Client.Messages_ExportChatInvite(peer, expireDate, memberLimit, name, request_needed: createsJoinRequest);
		return (await MakeChatInviteLink(exported))!;
	}

	/// <summary>Use this method to edit a non-primary invite link created by the bot. The bot must be an
	/// administrator in the chat for this to work and must have the appropriate admin rights</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="inviteLink">The invite link to edit</param>
	/// <param name="name">Invite link name; 0-32 characters</param>
	/// <param name="expireDate">Point in time when the link will expire</param>
	/// <param name="memberLimit">Maximum number of users that can be members of the chat simultaneously after joining the chat
	/// via this invite link; 1-99999</param>
	/// <param name="createsJoinRequest">Set to <see langword="true"/>, if users joining the chat via the link need to be approved by chat administrators.
	/// If <see langword="true"/>, <paramref name="memberLimit"/> can't be specified</param>
	/// <returns>Returns the edited invite link as a <see cref="ChatInviteLink"/> object.</returns>
	public async Task<ChatInviteLink> EditChatInviteLink(ChatId chatId, string inviteLink, string? name = default, DateTime? expireDate = default, int? memberLimit = default, bool createsJoinRequest = default)
	{
		var peer = await InputPeerChat(chatId);
		var result = await Client.Messages_EditExportedChatInvite(peer, inviteLink, expireDate, memberLimit, title: name, request_needed: createsJoinRequest);
		return (await MakeChatInviteLink(result.Invite))!;
	}

	/// <summary>Use this method to revoke an invite link created by the bot. If the primary link is revoked, a new
	/// link is automatically generated. The bot must be an administrator in the chat for this to work and
	/// must have the appropriate admin rights</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="inviteLink">The invite link to revoke</param>
	/// <returns>Returns the revoked invite link as <see cref="ChatInviteLink"/> object.</returns>
	public async Task<ChatInviteLink> RevokeChatInviteLink(ChatId chatId, string inviteLink)
	{
		var peer = await InputPeerChat(chatId);
		var result = await Client.Messages_EditExportedChatInvite(peer, inviteLink, revoked: true);
		return (await MakeChatInviteLink(result.Invite))!;
	}

	/// <summary>Use this method to approve or decline a chat join request. The bot must be an administrator in the chat for this to
	/// work and must have the <see cref="ChatPermissions.CanInviteUsers"/> administrator right.
	/// Returns <see langword="true"/> on success.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="approved">whether to approve or decline the chat join request</param>
	public async Task<bool> HideChatJoinRequest(ChatId chatId, long userId, bool approved)
	{
		var peer = await InputPeerChat(chatId);
		var user = InputPeerUser(userId);
		await Client.Messages_HideChatJoinRequest(peer, user, approved);
		return true;
	}

	/// <summary>Use this method to set (or delete) a new profile photo for the chat. Photos can't be changed for private chats.
	/// The bot must be an administrator in the chat for this to work and must have the appropriate admin rights</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="photo">New chat photo, or null to delete photo</param>
	public async Task SetChatPhoto(ChatId chatId, InputFileStream? photo)
	{
		var peer = await InputPeerChat(chatId);
		var inputPhoto = photo == null ? null : await InputChatPhoto(photo);
		await Client.EditChatPhoto(peer, inputPhoto);
	}

	/// <summary>Use this method to change the title of a chat. Titles can't be changed for private chats. The bot
	/// must be an administrator in the chat for this to work and must have the appropriate admin rights</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="title">New chat title, 1-255 characters</param>
	public async Task SetChatTitle(ChatId chatId, string title)
	{
		var peer = await InputPeerChat(chatId);
		try
		{
			await Client.EditChatTitle(peer, title);
		}
		catch (RpcException ex) when (ex.Message.EndsWith("_NOT_MODIFIED")) { }
	}

	/// <summary>Use this method to change the description of a group, a supergroup or a channel. The bot must
	/// be an administrator in the chat for this to work and must have the appropriate admin rights</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="description">New chat Description, 0-255 characters</param>
	public async Task SetChatDescription(ChatId chatId, string? description = default)
	{
		var peer = await InputPeerChat(chatId);
		await Client.Messages_EditChatAbout(peer, description);
	}

	/// <summary>Use this method to add/remove a message to the list of pinned messages in a chat. If the chat is not a private
	/// chat, the bot must be an administrator in the chat for this to work and must have the
	/// '<see cref="ChatMemberAdministrator.CanPinMessages"/>' admin right in a supergroup or
	/// '<see cref="ChatMemberAdministrator.CanEditMessages"/>' admin right in a channel</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of a message to pin or unpin. To unpin the most recent pinned message, pass 0</param>
	/// <param name="pin">whether to pin (true) or unpin (false)</param>
	/// <param name="disableNotification">Pass <c><see langword="true"/></c>, if it is not necessary to send a notification to all chat members about
	/// the new pinned message. Notifications are always disabled in channels and private chats</param>
	public async Task PinUnpinChatMessage(ChatId chatId, int messageId, bool pin = true, bool disableNotification = default)
	{
		var peer = await InputPeerChat(chatId);
		if (!pin && messageId == 0)
			if (peer is InputPeerUser user)
				messageId = (await Client.Users_GetFullUser(user)).full_user.pinned_msg_id;
			else
				messageId = (await Client.GetFullChat(peer)).full_chat.PinnedMsg;
		await Client.Messages_UpdatePinnedMessage(peer, messageId, silent: disableNotification, unpin: !pin);
	}

	/// <summary>Use this method to clear the list of pinned messages in a chat. If the chat is not a private chat,
	/// the bot must be an administrator in the chat for this to work and must have the
	/// '<see cref="ChatMemberAdministrator.CanPinMessages"/>' admin right in a supergroup or
	/// '<see cref="ChatMemberAdministrator.CanEditMessages"/>' admin right in a channel</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageThreadId">(optional) if you want to target only a specific forum topic</param>
	/// <remarks>Use messageThreadId=1 for the 'General' topic</remarks>
	public async Task UnpinAllMessages(ChatId chatId, int? messageThreadId = default)
	{
		var peer = await InputPeerChat(chatId);
		await Client.Messages_UnpinAllMessages(peer, messageThreadId);
	}

	/// <summary>Use this method for your bot to leave a group, supergroup or channel.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	public async Task LeaveChat(ChatId chatId)
	{
		var peer = await InputPeerChat(chatId);
		await Client.LeaveChat(peer);
	}

	/// <summary>Use this method to get up to date information about the chat (current name of the user for one-on-one
	/// conversations, current username of a user, group or channel, etc.)</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <returns>Returns a <see cref="Chat"/> object on success.</returns>
	public async Task<ChatFullInfo> GetChat(ChatId chatId)
	{
		if (chatId.Identifier is long userId && userId >= 0)
		{
			var inputUser = InputUser(userId);
			var userFull = await Client.Users_GetFullUser(inputUser);
			userFull.UserOrChat(_collector);
			var full = userFull.full_user;
			var user = userFull.users[userId];
			var chat = new ChatFullInfo
			{
				TLInfo = userFull,
				Id = user.id,
				Type = ChatType.Private,
				Username = user.MainUsername,
				FirstName = user.first_name,
				LastName = user.last_name,
				AccessHash = user.access_hash,
				AccentColorId = user.color?.flags.HasFlag(PeerColor.Flags.has_color) == true ? user.color.color : (int)(user.id % 7),
				Photo = (full.personal_photo ?? full.profile_photo ?? full.fallback_photo).ChatPhoto(),
				ActiveUsernames = user.username == null && user.usernames == null ? null : user.ActiveUsernames.ToArray(),
				Birthdate = full.birthday.Birthdate(),
				BusinessIntro = await MakeBusinessIntro(full.business_intro),
				BusinessLocation = full.business_location.BusinessLocation(),
				BusinessOpeningHours = full.business_work_hours.BusinessOpeningHours(),
				PersonalChat = full.personal_channel_id == 0 ? null : Chat(full.personal_channel_id),
				EmojiStatusCustomEmojiId = user.emoji_status?.document_id.ToString(),
				EmojiStatusExpirationDate = (user.emoji_status as EmojiStatusUntil)?.until,
				Bio = full.about,
				HasPrivateForwards = full.private_forward_name != null,
				HasRestrictedVoiceAndVideoMessages = user.flags.HasFlag(TL.User.Flags.premium) && full.flags.HasFlag(UserFull.Flags.voice_messages_forbidden),
				MessageAutoDeleteTime = full.ttl_period == 0 ? null : full.ttl_period
			};
			if (user.color?.flags.HasFlag(PeerColor.Flags.has_background_emoji_id) == true) chat.BackgroundCustomEmojiId = user.color.background_emoji_id.ToString();
			if (user.profile_color?.flags.HasFlag(PeerColor.Flags.has_color) == true) chat.ProfileAccentColorId = user.profile_color.color;
			if (user.profile_color?.flags.HasFlag(PeerColor.Flags.has_background_emoji_id) == true) chat.ProfileBackgroundCustomEmojiId = user.profile_color.background_emoji_id.ToString();
			if (full.pinned_msg_id > 0)
				chat.PinnedMessage = await GetMessage(inputUser, full.pinned_msg_id);
			return chat;
		}
		else
		{
			var inputPeer = await InputPeerChat(chatId);
			var mcf = await Client.GetFullChat(inputPeer);
			mcf.UserOrChat(_collector);
			var full = mcf.full_chat;
			var tlChat = mcf.chats[inputPeer.ID];
			var chat = new ChatFullInfo
			{
				TLInfo = mcf,
				Id = -tlChat.ID,
				Type = ChatType.Group,
				Title = tlChat.Title,
				Photo = full.ChatPhoto.ChatPhoto(),
				AvailableReactions = full.AvailableReactions switch
				{
					/*chatReactionsNone*/ null => [],
					ChatReactionsSome crs => crs.reactions.Select(TypesTLConverters.ReactionType).ToArray(),
					/*chatReactionsAll*/ _ => null,
				},
				MaxReactionCount = full.AvailableReactions == null ? 0 : Reactions_uniq_max,
				Description = full.About,
				InviteLink = (full.ExportedInvite as ChatInviteExported)?.link,
				MessageAutoDeleteTime = full.TtlPeriod == 0 ? null : full.TtlPeriod,
				AccentColorId = (int)(tlChat.ID % 7)
			};
			if (full.PinnedMsg > 0)
				chat.PinnedMessage = await GetMessage(inputPeer, full.PinnedMsg);
			if (tlChat is TL.Channel channel)
			{
				chat.Id = ZERO_CHANNEL_ID - tlChat.ID;
				chat.Type = channel.IsChannel ? ChatType.Channel : ChatType.Supergroup;
				chat.Username = channel.MainUsername;
				chat.IsForum = channel.flags.HasFlag(Channel.Flags.forum);
				chat.AccessHash = channel.access_hash;
				var channelFull = (ChannelFull)full;
				if (channelFull.flags2.HasFlag(ChannelFull.Flags2.has_reactions_limit)) chat.MaxReactionCount = channelFull.reactions_limit;
				chat.ActiveUsernames = channel.username == null && channel.usernames == null ? null : channel.ActiveUsernames.ToArray();
				if (channel.color?.flags.HasFlag(PeerColor.Flags.has_color) == true) chat.AccentColorId = channel.color.color;
				if (channel.color?.flags.HasFlag(PeerColor.Flags.has_background_emoji_id) == true) chat.BackgroundCustomEmojiId = channel.color.background_emoji_id.ToString();
				if (channel.profile_color?.flags.HasFlag(PeerColor.Flags.has_color) == true) chat.ProfileAccentColorId = channel.profile_color.color;
				if (channel.profile_color?.flags.HasFlag(PeerColor.Flags.has_background_emoji_id) == true) chat.ProfileBackgroundCustomEmojiId = channel.profile_color.background_emoji_id.ToString();
				chat.EmojiStatusCustomEmojiId = channel.emoji_status?.document_id.ToString();
				chat.EmojiStatusExpirationDate = (channel.emoji_status as EmojiStatusUntil)?.until;
				chat.JoinToSendMessages = channel.flags.HasFlag(Channel.Flags.join_to_send) || !channel.flags.HasFlag(Channel.Flags.megagroup) || channelFull.linked_chat_id == 0;
				chat.JoinByRequest = channel.flags.HasFlag(Channel.Flags.join_request);
				chat.Permissions = (channel.banned_rights ?? channel.default_banned_rights).ChatPermissions();
				chat.SlowModeDelay = channelFull.slowmode_seconds == 0 ? null : channelFull.slowmode_seconds;
				chat.UnrestrictBoostCount = channelFull.boosts_unrestrict == 0 ? null : channelFull.boosts_unrestrict;
				chat.HasAggressiveAntiSpamEnabled = channelFull.flags2.HasFlag(ChannelFull.Flags2.antispam);
				chat.HasHiddenMembers = channelFull.flags2.HasFlag(ChannelFull.Flags2.participants_hidden);
				chat.HasVisibleHistory = !channelFull.flags.HasFlag(ChannelFull.Flags.hidden_prehistory);
				chat.HasProtectedContent = channel.flags.HasFlag(Channel.Flags.noforwards);
				chat.StickerSetName = channelFull.stickerset?.short_name;
				chat.CanSetStickerSet = channelFull.flags.HasFlag(ChannelFull.Flags.can_set_stickers);
				chat.CustomEmojiStickerSetName = channelFull.emojiset?.short_name;
				chat.LinkedChatId = channelFull.linked_chat_id == 0 ? null : ZERO_CHANNEL_ID - channelFull.linked_chat_id;
				chat.Location = channelFull.location.ChatLocation();
			}
			else if (tlChat is TL.Chat basicChat)
			{
				chat.Permissions = basicChat.default_banned_rights.ChatPermissions();
				chat.HasProtectedContent = basicChat.flags.HasFlag(TL.Chat.Flags.noforwards);
				var chatFull = (ChatFull)full;
				if (chatFull.flags.HasFlag(ChatFull.Flags.has_reactions_limit)) chat.MaxReactionCount = chatFull.reactions_limit;
			}
			return chat;
		}
	}

	/// <summary>Use this method to get a list of administrators in a chat.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <returns>On success, returns an Array of <see cref="ChatMember"/> objects that contains information about all chat
	/// administrators except other bots. If the chat is a group or a supergroup and no administrators were
	/// appointed, only the creator will be returned</returns>
	public async Task<ChatMember[]> GetChatAdministrators(ChatId chatId)
	{
		InputPeer chat = await InputPeerChat(chatId);
		if (chat is InputPeerChannel ipc)
		{
			var participants = await Client.Channels_GetParticipants(ipc, new ChannelParticipantsAdmins());
			participants.UserOrChat(_collector);
			return await participants.participants.Select(async p => p.ChatMember(await UserOrResolve(p.UserId))).WhenAllSequential();
		}
		else
		{
			var full = await Client.Messages_GetFullChat(chat.ID);
			full.UserOrChat(_collector);
			if (full.full_chat is not ChatFull { participants: ChatParticipants participants })
				throw new WTException($"Cannot fetch participants for chat {chatId}");
			return await participants.participants.Where(p => p.IsAdmin).Select(async p => p.ChatMember(await UserOrResolve(p.UserId))).WhenAllSequential();
		}
	}

	/// <summary>Use this method to get the number of members in a chat.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <returns>Returns <see cref="int"/> on success.</returns>
	public async Task<int> GetChatMemberCount(ChatId chatId)
	{
		var inputPeer = await InputPeerChat(chatId);
		if (inputPeer is InputPeerUser) return 2;
		var chatFull = await Client.GetFullChat(inputPeer);
		chatFull.UserOrChat(_collector);
		return chatFull.full_chat.ParticipantsCount;
	}


	/// <summary>Use this method to get information about a member of a chat.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <returns>Returns a <see cref="ChatMember"/> object on success.</returns>
	public async Task<ChatMember> GetChatMember(ChatId chatId, long userId)
	{
		var chat = await InputPeerChat(chatId);
		if (chat is InputPeerChannel channel)
		{
			var user = InputPeerUser(userId);
			var part = await Client.Channels_GetParticipant(channel, user);
			part.UserOrChat(_collector);
			return part.participant.ChatMember(await UserOrResolve(userId));
		}
		else
		{
			var full = await Client.Messages_GetFullChat(chat.ID);
			full.UserOrChat(_collector);
			if (full.full_chat is not ChatFull { participants: ChatParticipants participants })
				throw new WTException($"Cannot fetch participants for chat {chatId}");
			var participant = participants.participants.FirstOrDefault(p => p.UserId == userId) ?? throw new WTException($"user not found ({userId} in chat {chatId})");
			return participant.ChatMember(await UserOrResolve(userId));
		}
	}


	/// <summary>Use this method to delete or set a new group sticker set for a supergroup. The bot must be an administrator in the
	/// chat for this to work and must have the appropriate admin rights. Use the field <see cref="Chat.CanSetStickerSet"/> optionally 
	/// returned in <see cref="GetChat(GetChatRequest)"/> requests to check if the bot can use this method.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="stickerSetName">Name of the sticker set to be set as the group sticker set (null to delete)</param>
	public async Task SetChatStickerSet(ChatId chatId, string? stickerSetName)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_SetStickers(channel, stickerSetName);
	}

	/// <summary>Use this method to get custom emoji stickers, which can be used as a forum topic icon by any user.</summary>
	/// <returns>Returns an Array of <see cref="Sticker"/> objects.</returns>
	public async Task<Sticker[]> GetForumTopicIconStickers()
	{
		var mss = await Client.Messages_GetStickerSet(new InputStickerSetEmojiDefaultTopicIcons());
		CacheStickerSet(mss);
		var stickers = await mss.documents.OfType<TL.Document>().Select(doc => MakeSticker(doc, doc.GetAttribute<DocumentAttributeSticker>())).WhenAllSequential();
		return stickers;
	}

	/// <summary>Use this method to create a topic in a forum supergroup chat. The bot must be an administrator in the chat for
	/// this to work and must have the <see cref="ChatAdministratorRights.CanManageTopics"/> administrator rights.
	/// Returns information about the created topic as a <see cref="ForumTopic"/> object.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="name">Topic name, 1-128 characters</param>
	/// <param name="iconColor">Color of the topic icon in RGB format. Currently, must be one of 7322096 (0x6FB9F0), 16766590 (0xFFD67E),
	/// 13338331 (0xCB86DB), 9367192 (0x8EEE98), 16749490 (0xFF93B2), or 16478047 (0xFB6F5F)</param>
	/// <param name="iconCustomEmojiId">Unique identifier of the custom emoji shown as the topic icon. Use
	/// <see cref="GetForumTopicIconStickers(GetForumTopicIconStickersRequest)"/> to get all allowed custom emoji identifiers</param>
	/// <returns>Returns information about the created topic as a <see cref="ForumTopic"/> object.</returns>
	public async Task<ForumTopic> CreateForumTopic(ChatId chatId, string name, Color? iconColor = default, string? iconCustomEmojiId = default)
	{
		var channel = await InputChannel(chatId);
		var msg = await PostedMsg(Client.Channels_CreateForumTopic(channel, name, Helpers.RandomLong(), iconColor?.ToInt(),
			icon_emoji_id: iconCustomEmojiId == null ? null : long.Parse(iconCustomEmojiId)), channel);
		var ftc = msg.ForumTopicCreated ?? throw new WTException("Channels_CreateForumTopic didn't result in ForumTopicCreated service message");
		return new ForumTopic { MessageThreadId = msg.MessageId, Name = ftc.Name, IconColor = ftc.IconColor, IconCustomEmojiId = ftc.IconCustomEmojiId };
	}

	/// <summary>Use this method to edit name and icon of a topic in a forum supergroup chat. The bot must be an administrator
	/// in the chat for this to work and must have <see cref="ChatAdministratorRights.CanManageTopics"/> administrator
	/// rights, unless it is the creator of the topic. Returns <see langword="true"/> on success.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread of the forum topic</param>
	/// <param name="name">New topic name, 0-128 characters. If not specified or empty, the current name of the topic will be kept</param>
	/// <param name="iconCustomEmojiId">New unique identifier of the custom emoji shown as the topic icon. Use
	/// <see cref="GetForumTopicIconStickersRequest"/> to get all allowed custom emoji identifiers. Pass an empty
	/// string to remove the icon. If not specified, the current icon will be kept</param>
	/// <remarks>Use messageThreadId=1 for the 'General' topic</remarks>
	public async Task EditForumTopic(ChatId chatId, int messageThreadId, string? name = default, string? iconCustomEmojiId = default)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_EditForumTopic(channel, messageThreadId, name, iconCustomEmojiId == null ? null : 
			iconCustomEmojiId == "" ? 0 : long.Parse(iconCustomEmojiId));
	}

	/// <summary>Use this method to close or reopen a topic in a forum supergroup chat. The bot must be an administrator in the chat
	/// for this to work and must have the <see cref="ChatAdministratorRights.CanManageTopics"/> administrator rights,
	/// unless it is the creator of the topic. Returns <see langword="true"/> on success.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread of the forum topic</param>
	/// <param name="closed">whether to close (true) or reopen (false) the topic</param>
	/// <remarks>Use messageThreadId=1 for the 'General' topic</remarks>
	public async Task CloseReopenForumTopic(ChatId chatId, int messageThreadId, bool closed = true)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_EditForumTopic(channel, messageThreadId, closed: closed);
	}

	/// <summary>Use this method to delete a forum topic along with all its messages in a forum supergroup chat. The bot must be
	/// an administrator in the chat for this to work and must have the
	/// <see cref="ChatAdministratorRights.CanManageTopics"/> administrator rights. Returns <see langword="true"/> on success.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread of the forum topic</param>
	public async Task DeleteForumTopic(ChatId chatId, int messageThreadId)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_DeleteTopicHistory(channel, messageThreadId);
	}

	/// <summary>Use this method to hide the 'General' topic in a forum supergroup chat. The bot must be an administrator in the
	/// chat for this to work and must have the <see cref="ChatAdministratorRights.CanManageTopics"/> administrator
	/// rights. The topic will be automatically closed if it was open. Returns <see langword="true"/> on success.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="hidden">true to hide, false to unhide</param>
	public async Task HideGeneralForumTopic(ChatId chatId, bool hidden = true)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_EditForumTopic(channel, 1, hidden: hidden);
	}

	/// <summary>Use this method to send answers to callback queries sent from
	/// <see cref="InlineKeyboardMarkup">inline keyboards</see>. The answer will be displayed
	/// to the user as a notification at the top of the chat screen or as an alert</summary>
	/// <remarks>Alternatively, the user can be redirected to the specified Game URL.For this option to work, you must
	/// first create a game for your bot via <c>@BotFather</c> and accept the terms. Otherwise, you may use
	/// links like <c>t.me/your_bot?start=XXXX</c> that open your bot with a parameter</remarks>
	/// <param name="callbackQueryId">Unique identifier for the query to be answered</param>
	/// <param name="text">Text of the notification. If not specified, nothing will be shown to the user, 0-200 characters</param>
	/// <param name="showAlert">If <see langword="true"/>, an alert will be shown by the client instead of a notification at the top of the chat screen.</param>
	/// <param name="url">URL that will be opened by the user's client. If you have created a
	/// <a href="https://core.telegram.org/bots/api#game">Game</a> and accepted the conditions via
	/// <c>@BotFather</c>, specify the URL that opens your game — note that this will only work if the query comes from a callback_game button
	/// <para>Otherwise, you may use links like <c>t.me/your_bot?start=XXXX</c> that open your bot with a parameter</para></param>
	/// <param name="cacheTime">The maximum amount of time in seconds that the result of the callback query may be cached client-side.
	/// Telegram apps will support caching starting in version 3.14</param>
	public async Task AnswerCallbackQuery(string callbackQueryId, string? text = default, bool showAlert = default, string? url = default, int cacheTime = 0)
	{
		await Client.Messages_SetBotCallbackAnswer(long.Parse(callbackQueryId), cacheTime, text, url, showAlert);
	}

	/// <summary>Use this method to get the list of boosts added to a chat by a user. Requires administrator rights in the chat.</summary>
	/// <param name="chatId">Unique identifier for the chat or username of the channel (in the format <c>@channelusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <returns>Returns a <see cref="UserChatBoosts"/> object.</returns>
	public async Task<UserChatBoosts> GetUserChatBoosts(ChatId chatId, long userId)
	{
		var peer = await InputPeerChat(chatId);
		var boosts = await Client.Premium_GetUserBoosts(peer, InputUser(userId));
		return new UserChatBoosts { Boosts = await boosts.boosts.Select(MakeBoost).WhenAllSequential() };
	}

	/// <summary>Use this method to get information about the connection of the bot with a business account.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <returns>Returns a <see cref="BusinessConnection"/> object.</returns>
	public async Task<BusinessConnection> GetBusinessConnection(string businessConnectionId)
	{
		var updates = await Client.Account_GetBotBusinessConnection(businessConnectionId);
		updates.UserOrChat(_collector);
		var conn = updates.UpdateList.OfType<UpdateBotBusinessConnect>().First().connection;
		return await MakeBusinessConnection(conn);
	}

	/// <summary>Use this method to change the list of the bot’s commands.
	/// See <a href="https://core.telegram.org/bots#commands"/> for more details about bot commands</summary>
	/// <param name="commands">A list of bot commands to be set as the list of the bot’s commands. At most 100 commands can be specified</param>
	/// <param name="scope">An object, describing scope of users for which the commands are relevant. Defaults to <see cref="BotCommandScopeDefault"/>.</param>
	/// <param name="languageCode">A two-letter ISO 639-1 language code. If empty, commands will be applied to all users from the given
	/// <paramref name="scope"/>, for whose language there are no dedicated commands</param>
	public async Task SetMyCommands(IEnumerable<BotCommand> commands, BotCommandScope? scope = default, string? languageCode = default)
	{
		await Client.Bots_SetBotCommands(await BotCommandScope(scope), languageCode, commands.Select(TypesTLConverters.BotCommand).ToArray());
	}

	/// <summary>Use this method to delete the list of the bot’s commands for the given <paramref name="scope"/> and
	/// <paramref name="languageCode">user language</paramref>. After deletion,
	/// <a href="https://core.telegram.org/bots/api#determining-list-of-commands">higher level commands</a> will be shown to affected users</summary>
	/// <param name="scope">An object, describing scope of users for which the commands are relevant. Defaults to <see cref="BotCommandScopeDefault"/>.</param>
	/// <param name="languageCode">A two-letter ISO 639-1 language code. If empty, commands will be applied to all users from the given
	/// <paramref name="scope"/>, for whose language there are no dedicated commands</param>
	public async Task DeleteMyCommands(BotCommandScope? scope = default, string? languageCode = default)
	{
		await Client.Bots_ResetBotCommands(await BotCommandScope(scope), languageCode);
	}

	/// <summary>Use this method to get the current list of the bot’s commands for the given <paramref name="scope"/> and
	/// <paramref name="languageCode">user language</paramref></summary>
	/// <param name="scope">An object, describing scope of users. Defaults to <see cref="BotCommandScopeDefault"/>.</param>
	/// <param name="languageCode">A two-letter ISO 639-1 language code or an empty string</param>
	/// <returns>Returns Array of <see cref="BotCommand"/> on success. If commands aren't set, an empty list is returned</returns>
	public async Task<BotCommand[]> GetMyCommands(BotCommandScope? scope = default, string? languageCode = default)
	{
		var commands = await Client.Bots_GetBotCommands(await BotCommandScope(scope), languageCode);
		return commands.Select(TypesTLConverters.BotCommand).ToArray();
	}

	/// <summary>Use this method to change the bot's name, short description (bio) or description (shown in empty chat).</summary>
	/// <param name="name">New bot name; 0-64 characters. Unchanged if null. Pass an empty string to remove the dedicated name for the given language.</param>
	/// <param name="shortDescription">New short description for the bot; 0-120 characters. Unchanged if null. Pass an empty string to remove the dedicated short description for the given language.</param>
	/// <param name="description">New bot description; 0-512 characters. Unchanged if null. Pass an empty string to remove the dedicated description for the given language.</param>
	/// <param name="languageCode">A two-letter ISO 639-1 language code. If empty, the name will be shown to all users for whose language
	/// there is no dedicated name.</param>
	public async Task SetMyInfo(string? name = default, string? shortDescription = default, string? description = default, string? languageCode = default)
	{
		await Client.Bots_SetBotInfo(languageCode, name: name, about: shortDescription, description: description);
	}

	/// <summary>Use this method to get the current bot name for the given user language.</summary>
	/// <param name="languageCode">A two-letter ISO 639-1 language code or an empty string</param>
	/// <returns>Returns <see cref="BotName"/> on success.</returns>
	public async Task<(string name, string shortDescription, string description)> GetMyInfo(string? languageCode = default)
	{
		var botInfo = await Client.Bots_GetBotInfo(languageCode);
		return (botInfo.name, botInfo.about, botInfo.description);
	}

	/// <summary>Use this method to change the bot’s menu button in a private chat, or the default menu button.</summary>
	/// <param name="chatId">Unique identifier for the target private chat. If not specified, default bot’s menu button will be changed</param>
	/// <param name="menuButton">An object for the new bot’s menu button. Defaults to <see cref="MenuButtonDefault"/></param>
	public async Task SetChatMenuButton(long? chatId = default, MenuButton? menuButton = default)
	{
		var user = chatId.HasValue ? InputUser(chatId.Value) : null;
		await Client.Bots_SetBotMenuButton(user, menuButton.BotMenuButton());
	}

	/// <summary>Use this method to get the current value of the bot’s menu button in a private chat, or the default menu button.</summary>
	/// <param name="chatId">Unique identifier for the target private chat. If not specified, default bot’s menu button will be returned</param>
	/// <returns><see cref="MenuButton"/> set for the given chat id or a default one</returns>
	public async Task<MenuButton> GetChatMenuButton(long? chatId = default)
	{
		var user = chatId.HasValue ? InputUser(chatId.Value) : null;
		var botMenuButton = await Client.Bots_GetBotMenuButton(user);
		return botMenuButton.MenuButton();
	}

	/// <summary>Use this method to change the default administrator rights requested by the bot when it's added as an administrator 
	/// to groups or channels. These rights will be suggested to users, but they are free to modify the list before adding the bot.</summary>
	/// <param name="rights">An object describing new default administrator rights. If not specified, the default administrator rights will be cleared.</param>
	/// <param name="forChannels">Pass <see langword="true"/> to change the default administrator rights of the bot in channels. Otherwise, the default
	/// administrator rights of the bot for groups and supergroups will be changed.</param>
	public async Task SetMyDefaultAdministratorRights(ChatAdministratorRights? rights = default, bool forChannels = default)
	{
		var admin_rights = rights.ChatAdminRights();
		if (forChannels)
			await Client.Bots_SetBotBroadcastDefaultAdminRights(admin_rights);
		else
			await Client.Bots_SetBotGroupDefaultAdminRights(admin_rights);
	}

	/// <summary>Use this method to get the current default administrator rights of the bot.</summary>
	/// <param name="forChannels">Pass <see langword="true"/> to change the default administrator rights of the bot in channels. Otherwise, the default
	/// administrator rights of the bot for groups and supergroups will be changed.</param>
	/// <returns>Default or channel <see cref="ChatAdministratorRights"/> </returns>
	public async Task<ChatAdministratorRights> GetMyDefaultAdministratorRights(bool forChannels = default)
	{
		var full = await Client.Users_GetFullUser(Client.User);
		return (forChannels ? full.full_user.bot_broadcast_admin_rights : full.full_user.bot_group_admin_rights).ChatAdministratorRights();
	}
	#endregion Available methods

	#region Updating messages

	/// <summary>Use this method to edit text and game messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="text">New text of the message, 1-4096 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="entities">List of special entities that appear in message text, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="linkPreviewOptions">Link preview generation options for the message</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <returns>On success the edited <see cref="Message"/> is returned.</returns>
	public async Task<Message> EditMessageText(ChatId chatId, int messageId, string text, ParseMode parseMode = default, IEnumerable<MessageEntity>? entities = default, LinkPreviewOptions? linkPreviewOptions = default, InlineKeyboardMarkup? replyMarkup = default)
	{
		ApplyParse(parseMode, ref text!, ref entities);
		var peer = await InputPeerChat(chatId);
		var media = linkPreviewOptions.InputMediaWebPage();
		return await PostedMsg(Client.Messages_EditMessage(peer, messageId, text, media,
			await MakeReplyMarkup(replyMarkup), entities?.ToArray(), no_webpage: linkPreviewOptions?.IsDisabled == true, invert_media: linkPreviewOptions?.ShowAboveText == true), peer, text);
	}

	/// <summary>Use this method to edit text and game messages.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="text">New text of the message, 1-4096 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="entities">List of special entities that appear in message text, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="linkPreviewOptions">Link preview generation options for the message</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	public async Task EditMessageText(string inlineMessageId, string text, ParseMode parseMode = default, IEnumerable<MessageEntity>? entities = default, LinkPreviewOptions? linkPreviewOptions = default, InlineKeyboardMarkup? replyMarkup = default)
	{
		ApplyParse(parseMode, ref text!, ref entities);
		var id = inlineMessageId.ParseInlineMsgID();
		var media = linkPreviewOptions.InputMediaWebPage();
		await Client.Messages_EditInlineBotMessage(id, text, media,
			await MakeReplyMarkup(replyMarkup), entities?.ToArray(), linkPreviewOptions?.IsDisabled == true, linkPreviewOptions?.ShowAboveText == true);
	}

	/// <summary>Use this method to edit captions of messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="caption">New caption of the message, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media. Ignored if a new caption isn't specified.</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <returns>On success the edited <see cref="Message"/> is returned.</returns>
	public async Task<Message> EditMessageCaption(ChatId chatId, int messageId, string? caption, ParseMode parseMode = default, 
		IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, InlineKeyboardMarkup? replyMarkup = default)
	{
		ApplyParse(parseMode, ref caption!, ref captionEntities);
		var peer = await InputPeerChat(chatId);
		return await PostedMsg(Client.Messages_EditMessage(peer, messageId, caption, null,
			await MakeReplyMarkup(replyMarkup), captionEntities?.ToArray(), invert_media: showCaptionAboveMedia), peer, caption);
	}

	/// <summary>Use this method to edit captions of messages.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="caption">New caption of the message, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See
	/// <a href="https://core.telegram.org/bots/api#formatting-options">formatting</a> options for more details</param>
	/// <param name="captionEntities">List of special entities that appear in the caption, which can be specified instead of <see cref="ParseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media. Ignored if a new caption isn't specified.</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	public async Task EditMessageCaption(string inlineMessageId, string? caption, ParseMode parseMode = default,
		IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, InlineKeyboardMarkup? replyMarkup = default)
	{
		ApplyParse(parseMode, ref caption!, ref captionEntities);
		var id = inlineMessageId.ParseInlineMsgID();
		await Client.Messages_EditInlineBotMessage(id, caption, null, await MakeReplyMarkup(replyMarkup), captionEntities?.ToArray(), invert_media: showCaptionAboveMedia);
	}

	/// <summary>Use this method to edit animation, audio, document, photo, or video messages. If a message is part of
	/// a message album, then it can be edited only to an audio for audio albums, only to a document for document
	/// albums and to a photo or a video otherwise. Use a previously uploaded file via its <see cref="InputFileId"/> or specify a URL</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="media">A new media content of the message</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <returns>On success the edited <see cref="Message"/> is returned.</returns>
	public async Task<Message> EditMessageMedia(ChatId chatId, int messageId, InputMedia media, InlineKeyboardMarkup? replyMarkup = default)
	{
		var peer = await InputPeerChat(chatId);
		var ism = await InputSingleMedia(media);
		return await PostedMsg(Client.Messages_EditMessage(peer, messageId, ism.message ?? "", ism.media,
			await MakeReplyMarkup(replyMarkup), ism.entities), peer);
	}

	/// <summary>Use this method to edit animation, audio, document, photo, or video messages. If a message is part of
	/// a message album, then it can be edited only to an audio for audio albums, only to a document for document
	/// albums and to a photo or a video otherwise. Use a previously uploaded file via its <see cref="InputFileId"/> or specify a URL</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="media">A new media content of the message</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	public async Task EditMessageMedia(string inlineMessageId, InputMedia media, InlineKeyboardMarkup? replyMarkup = default)
	{
		var id = inlineMessageId.ParseInlineMsgID();
		var ism = await InputSingleMedia(media);
		await Client.Messages_EditInlineBotMessage(id, ism.message ?? "", ism.media,
			await MakeReplyMarkup(replyMarkup), ism.entities);
	}

	/// <summary>Use this method to edit live location messages. A location can be edited until its
	/// <see cref="Location.LivePeriod"/> expires or editing is explicitly disabled by a call to
	/// <see cref="StopMessageLiveLocation( ChatId, int, InlineKeyboardMarkup?)"/>.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="latitude">Latitude of new location</param>
	/// <param name="longitude">Longitude of new location</param>
	/// <param name="livePeriod">Period in seconds for which the location will be updated, should be between 60 and 86400</param>
	/// <param name="horizontalAccuracy">The radius of uncertainty for the location, measured in meters; 0-1500</param>
	/// <param name="heading">Direction in which the user is moving, in degrees. Must be between 1 and 360 if specified</param>
	/// <param name="proximityAlertRadius">Maximum distance for proximity alerts about approaching another chat member, in meters.
	/// Must be between 1 and 100000 if specified</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <returns>On success the edited <see cref="Message"/> is returned.</returns>
	public async Task<Message> EditMessageLiveLocation(ChatId chatId, int messageId, double latitude, double longitude, int livePeriod = 0,
		int horizontalAccuracy = 0, int heading = 0, int proximityAlertRadius = 0, InlineKeyboardMarkup? replyMarkup = default)
	{
		var peer = await InputPeerChat(chatId);
		var media = MakeGeoLive(latitude, longitude, horizontalAccuracy, heading, proximityAlertRadius, livePeriod);
		return await PostedMsg(Client.Messages_EditMessage(peer, messageId, null, media, await MakeReplyMarkup(replyMarkup)), peer);
	}

	/// <summary>Use this method to edit live location messages. A location can be edited until its
	/// <see cref="Location.LivePeriod"/> expires or editing is explicitly disabled by a call to
	/// <see cref="StopMessageLiveLocation( string, InlineKeyboardMarkup?)"/>.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="latitude">Latitude of new location</param>
	/// <param name="longitude">Longitude of new location</param>
	/// <param name="horizontalAccuracy">The radius of uncertainty for the location, measured in meters; 0-1500</param>
	/// <param name="livePeriod">Period in seconds for which the location will be updated, should be between 60 and 86400</param>
	/// <param name="heading">Direction in which the user is moving, in degrees. Must be between 1 and 360 if specified</param>
	/// <param name="proximityAlertRadius">Maximum distance for proximity alerts about approaching another chat member, in meters.
	/// Must be between 1 and 100000 if specified</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	public async Task EditMessageLiveLocation(string inlineMessageId, double latitude, double longitude, int livePeriod = 0,
		int horizontalAccuracy = 0, int heading = 0, int proximityAlertRadius = 0, InlineKeyboardMarkup? replyMarkup = default)
	{
		var id = inlineMessageId.ParseInlineMsgID();
		var media = MakeGeoLive(latitude, longitude, horizontalAccuracy, heading, proximityAlertRadius, livePeriod);
		await Client.Messages_EditInlineBotMessage(id, null, media, await MakeReplyMarkup(replyMarkup));
	}

	/// <summary>Use this method to stop updating a live location message before
	/// <see cref="Location.LivePeriod"/> expires.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the sent message</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <returns>On success the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> StopMessageLiveLocation(ChatId chatId, int messageId, InlineKeyboardMarkup? replyMarkup = default)
	{
		var peer = await InputPeerChat(chatId);
		var media = new InputMediaGeoLive { flags = InputMediaGeoLive.Flags.stopped };
		return await PostedMsg(Client.Messages_EditMessage(peer, messageId, null, media, await MakeReplyMarkup(replyMarkup)), peer);
	}

	/// <summary>Use this method to stop updating a live location message before
	/// <see cref="Location.LivePeriod"/> expires.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	public async Task StopMessageLiveLocation(string inlineMessageId, InlineKeyboardMarkup? replyMarkup = default)
	{
		var id = inlineMessageId.ParseInlineMsgID();
		var media = new InputMediaGeoLive { flags = InputMediaGeoLive.Flags.stopped };
		await Client.Messages_EditInlineBotMessage(id, null, media, await MakeReplyMarkup(replyMarkup));
	}

	/// <summary>Use this method to edit only the reply markup of messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <returns>On success the edited <see cref="Message"/> is returned.</returns>
	public async Task<Message> EditMessageReplyMarkup(ChatId chatId, int messageId, InlineKeyboardMarkup? replyMarkup = default)
	{
		var peer = await InputPeerChat(chatId);
		return await PostedMsg(Client.Messages_EditMessage(peer, messageId, null, null, await MakeReplyMarkup(replyMarkup)), peer);
	}

	/// <summary>Use this method to edit only the reply markup of messages.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	public async Task EditMessageReplyMarkup(string inlineMessageId, InlineKeyboardMarkup? replyMarkup = default)
	{
		var id = inlineMessageId.ParseInlineMsgID();
		await Client.Messages_EditInlineBotMessage(id, reply_markup: await MakeReplyMarkup(replyMarkup));
	}

	/// <summary>Use this method to stop a poll which was sent by the bot.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the original message with the poll</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <returns>On success, the stopped <see cref="Poll"/> with the final results is returned.</returns>
	public async Task<Telegram.Bot.Types.Poll> StopPoll(ChatId chatId, int messageId, InlineKeyboardMarkup? replyMarkup = default)
	{
		var peer = await InputPeerChat(chatId);
		var closedPoll = new InputMediaPoll { poll = new() { flags = TL.Poll.Flags.closed } };
		var updates = await Client.Messages_EditMessage(peer, messageId, null, closedPoll, await MakeReplyMarkup(replyMarkup));
		updates.UserOrChat(_collector);
		var ump = updates.UpdateList.OfType<UpdateMessagePoll>().First();
		return MakePoll(ump.poll, ump.results);
	}

	/// <summary>Use this method to delete a message, including service messages, with the following limitations:
	/// <list type="bullet"><item>A message can only be deleted if it was sent less than 48 hours ago</item>
	/// <item>A dice message in a private chat can only be deleted if it was sent more than 24 hours ago</item>
	/// <item>Bots can delete outgoing messages in private chats, groups, and supergroups</item>
	/// <item>Bots can delete incoming messages in private chats</item>
	/// <item>Bots granted can_post_messages permissions can delete outgoing messages in channels</item>
	/// <item>If the bot is an administrator of a group, it can delete any message there</item>
	/// <item>If the bot has can_delete_messages permission in a supergroup or a channel, it can delete any message there</item></list></summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageIds">Identifiers of 1-100 messages to delete</param>
	public async Task DeleteMessages(ChatId chatId, params int[] messageIds)
	{
		await Client.DeleteMessages(await InputPeerChat(chatId), messageIds);
	}
	#endregion Updating messages

	#region Stickers

	/// <summary>Use this method to send static .WEBP, animated .TGS, or video .WEBM stickers.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="sticker">Sticker to send. Pass a <see cref="InputFileId"/> as String to send a file that exists on the Telegram servers (recommended),
	/// pass an HTTP URL as a String for Telegram to get a .WEBP sticker from the Internet, or upload a new .WEBP or .TGS sticker.
	/// Video stickers can only be sent by a <see cref="InputFileId"/>. Animated stickers can't be sent via an HTTP URL.</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="emoji">Emoji associated with the sticker; only for just uploaded stickers</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the action will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendSticker(ChatId chatId, InputFile sticker, 
		ReplyParameters? replyParameters = default, IReplyMarkup? replyMarkup = default, string? emoji = default, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = await InputMediaDocument(sticker);
		if (media is TL.InputMediaUploadedDocument doc)
			doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeSticker { alt = emoji }];
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, disableNotification, protectContent, messageEffectId),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method to get a sticker set.</summary>
	/// <param name="name">Name of the sticker set</param>
	/// <returns>On success, a <see cref="StickerSet"/> object is returned.</returns>
	public async Task<Telegram.Bot.Types.StickerSet> GetStickerSet(string name)
	{
		var mss = await Client.Messages_GetStickerSet(name);
		CacheStickerSet(mss);
		var thumb = mss.set.thumbs?[0].PhotoSize(mss.set.ToFileLocation(mss.set.thumbs[0]), mss.set.thumb_dc_id);
		var stickers = await mss.documents.OfType<TL.Document>().Select(async doc =>
		{
			var sticker = await MakeSticker(doc, doc.GetAttribute<DocumentAttributeSticker>());
			if (thumb == null && doc.id == mss.set.thumb_document_id)
			{
				var thumbPhotoSize = new TL.PhotoSize() { type = "t", w = sticker.Width, h = sticker.Height, size = (int)doc.size };
				thumb = thumbPhotoSize.PhotoSize(doc.ToFileLocation(thumbPhotoSize), doc.dc_id);
			}
			return sticker;
		}).WhenAllSequential();
		return new Telegram.Bot.Types.StickerSet
		{
			Name = mss.set.short_name,
			Title = mss.set.title,
			StickerType = mss.set.flags.HasFlag(TL.StickerSet.Flags.emojis) ? StickerType.CustomEmoji :
							mss.set.flags.HasFlag(TL.StickerSet.Flags.masks) ? StickerType.Mask : StickerType.Regular,
#pragma warning disable CS0618 // Type or member is obsolete
			IsAnimated = stickers[0].IsAnimated, // mss.set.flags.HasFlag(TL.StickerSet.Flags.animated), was removed
			IsVideo = stickers[0].IsVideo, // mss.set.flags.HasFlag(TL.StickerSet.Flags.videos),
#pragma warning restore CS0618 // Type or member is obsolete
			Stickers = stickers,
			Thumbnail = thumb
		};
	}

	/// <summary>Use this method to get information about custom emoji stickers by their identifiers.
	/// Returns an Array of <see cref="Sticker"/> objects.</summary>
	/// <param name="customEmojiIds">List of custom emoji identifiers. At most 200 custom emoji identifiers can be specified.</param>
	/// <returns>On success, a <see cref="StickerSet"/> object is returned.</returns>
	public async Task<Sticker[]> GetCustomEmojiStickers(IEnumerable<string> customEmojiIds)
	{
		var documents = await Client.Messages_GetCustomEmojiDocuments(customEmojiIds.Select(long.Parse).ToArray());
		return await documents.OfType<TL.Document>().Select(async doc =>
		{
			var attrib = doc.GetAttribute<DocumentAttributeCustomEmoji>();
			return await MakeSticker(doc, null);
		}).WhenAllSequential();
	}

	/// <summary>Use this method to upload a file with a sticker for later use in the <see cref="CreateNewStickerSetRequest"/>
	/// and <see cref="AddStickerToSetRequest"/> methods (the file can be used multiple times).</summary>
	/// <param name="userId">User identifier of sticker file owner</param>
	/// <param name="sticker">A file with the sticker in .WEBP, .PNG, .TGS, or .WEBM format.</param>
	/// <param name="stickerFormat">Format of the sticker</param>
	/// <returns>Returns the uploaded <see cref="File"/> on success.</returns>
	public async Task<File> UploadStickerFile(long userId, InputFileStream sticker, StickerFormat stickerFormat)
	{
		var mimeType = MimeType(stickerFormat);
		var peer = InputPeerUser(userId);
		var uploadedFile = await Client.UploadFileAsync(sticker.Content, sticker.FileName);
		DocumentAttribute[] attribs = stickerFormat == StickerFormat.Animated ? [new DocumentAttributeSticker { }] : [];
		var media = new TL.InputMediaUploadedDocument(uploadedFile, mimeType, attribs);
		var messageMedia = await Client.Messages_UploadMedia(peer, media);
		if (messageMedia is not MessageMediaDocument { document: TL.Document doc })
			throw new WTException("Unexpected UploadMedia result");
		var file = new Telegram.Bot.Types.File { FileSize = doc.size }.SetFileIds(doc.ToFileLocation(), doc.dc_id);
		file.FilePath = file.FileId + '/' + sticker.FileName;
		return file;
	}

	/// <summary>Use this method to create a new sticker set owned by a user.</summary>
	/// <param name="userId">User identifier of created sticker set owner</param>
	/// <param name="name">Short name of sticker set, to be used in <c>t.me/addstickers/</c> URLs (e.g., <i>animals</i>). Can contain
	/// only English letters, digits and underscores. Must begin with a letter, can't contain consecutive
	/// underscores and must end in <i>"_by_&lt;bot username&gt;"</i>. <i>&lt;bot_username&gt;</i> is case insensitive. 1-64 characters</param>
	/// <param name="title">Sticker set title, 1-64 characters</param>
	/// <param name="stickers">A JSON-serialized list of 1-50 initial stickers to be added to the sticker set</param>
	/// <param name="stickerType">Type of stickers in the set. By default, a regular sticker set is created.</param>
	/// <param name="needsRepainting">Pass <see langword="true"/> if stickers in the sticker set must be repainted to the color of text
	/// when used in messages, the accent color if used as emoji status, white on chat photos, or another appropriate color based on context;
	/// for <see cref="StickerType.CustomEmoji">custom emoji</see> sticker sets only</param>
	public async Task CreateNewStickerSet(long userId, string name, string title, IEnumerable<InputSticker> stickers, StickerType? stickerType = default, bool needsRepainting = default)
	{
		var tlStickers = await Task.WhenAll(stickers.Select(sticker => InputStickerSetItem(userId, sticker)));
		var mss = await Client.Stickers_CreateStickerSet(InputPeerUser(userId), title, name, tlStickers, null, "bot" + BotId,
			stickerType == StickerType.Mask, stickerType == StickerType.CustomEmoji, needsRepainting);
		CacheStickerSet(mss);
	}

	/// <summary>Use this method to add a new sticker to a set created by the bot.
	/// The format of the added sticker must match the format of the other stickers in the set. <list type="bullet">
	/// <item>Emoji sticker sets can have up to 200 stickers.</item>
	/// <item>Animated and video sticker sets can have up to 50 stickers.</item>
	/// <item>Static sticker sets can have up to 120 stickers.</item></list></summary>
	/// <param name="userId">User identifier of sticker set owner</param>
	/// <param name="name">Sticker set name</param>
	/// <param name="sticker">A JSON-serialized object with information about the added sticker.
	/// If exactly the same sticker had already been added to the set, then the set isn't changed.</param>
	public async Task AddStickerToSet(long userId, string name, InputSticker sticker)
	{
		var tlSticker = await InputStickerSetItem(userId, sticker);
		var mss = await Client.Stickers_AddStickerToSet(name, tlSticker);
		CacheStickerSet(mss);
	}

	/// <summary>Use this method to move a sticker in a set created by the bot to a specific position.</summary>
	/// <param name="sticker"><see cref="InputFileId">File identifier</see> of the sticker</param>
	/// <param name="position">New sticker position in the set, zero-based</param>
	public async Task SetStickerPositionInSet(InputFileId sticker, int position)
	{
		var inputDoc = InputDocument(sticker.Id);
		await Client.Stickers_ChangeStickerPosition(inputDoc, position);
	}

	/// <summary>Use this method to delete a sticker from a set created by the bot.</summary>
	/// <param name="sticker"><see cref="InputFileId">File identifier</see> of the sticker</param>
	public async Task DeleteStickerFromSet(InputFileId sticker)
	{
		var inputDoc = InputDocument(sticker.Id);
		await Client.Stickers_RemoveStickerFromSet(inputDoc);
	}

	/// <summary>Use this method to replace an existing sticker in a sticker set with a new one. The method is equivalent to
	/// calling <see cref="DeleteStickerFromSet(Telegram.Bot.Telegram.Bot.Requests.DeleteStickerFromSetRequest,System.Threading.CancellationToken)"/>,
	/// then <see cref="AddStickerToSet(Telegram.Bot.Telegram.Bot.Requests.AddStickerToSetRequest,System.Threading.CancellationToken)"/>,
	/// then <see cref="SetStickerPositionInSet(Telegram.Bot.Telegram.Bot.Requests.SetStickerPositionInSetRequest,System.Threading.CancellationToken)"/>.
	/// Returns <see langword="true"/> on success.</summary>
	/// <param name="userId">User identifier of the sticker set owner</param>
	/// <param name="name">Sticker set name</param>
	/// <param name="oldSticker">File identifier of the replaced sticker</param>
	/// <param name="sticker">An object with information about the added sticker. If exactly the same sticker had already been added to the
	/// set, then the set remains unchanged.</param>
	public async Task ReplaceStickerInSet(long userId, string name, string oldSticker, InputSticker sticker)
	{
		var inputDoc = InputDocument(oldSticker);
		var tlSticker = await InputStickerSetItem(userId, sticker);
		var mss = await Client.Stickers_ReplaceSticker(inputDoc, tlSticker);
		CacheStickerSet(mss);
	}

	/// <summary>Use this method to change the list of emoji or the search keywords assigned to a regular or custom emoji sticker ;
	/// or to change the mask position of a mask sticker. The sticker must belong to a sticker set created by the bot.</summary>
	/// <param name="sticker"><see cref="InputFileId">File identifier</see> of the sticker</param>
	/// <param name="emojiList">(optional) A string composed of 1-20 emoji associated with the sticker</param>
	/// <param name="keywords">(optional) A comma-separated list of 0-20 search keywords for the sticker with total length of up to 64 characters. Pass an empty list to remove keywords.</param>
	/// <param name="maskPosition">(optional) An object with the position where the mask should be placed on faces. Pass null to remove the mask position.</param>
	public async Task SetStickerInfo(InputFileId sticker, string? emojiList = default, string? keywords = default, MaskPosition? maskPosition = default)
	{
		var inputDoc = InputDocument(sticker.Id);
		await Client.Stickers_ChangeSticker(inputDoc, emojiList, maskPosition.MaskCoord(), keywords);
	}

	/// <summary>Use this method to set the title of a created sticker set.</summary>
	/// <param name="name">Sticker set name</param>
	/// <param name="title">Sticker set title, 1-64 characters</param>
	public async Task SetStickerSetTitle(string name, string title)
	{
		await Client.Stickers_RenameStickerSet(name, title);
	}

	/// <summary>Use this method to set the thumbnail of a regular or mask sticker set.
	/// The format of the thumbnail file must match the format of the stickers in the set. Returns <see langword="true"/> on success.</summary>
	/// <param name="name">Sticker set name</param>
	/// <param name="userId">User identifier of the sticker set owner</param>
	/// <param name="format">Format of the thumbnail</param>
	/// <param name="thumbnail">A <b>.WEBP</b> or <b>.PNG</b> image with the thumbnail, must be up to 128 kilobytes in size and have
	/// a width and height of exactly 100px, or a <b>.TGS</b> animation with a thumbnail up to 32 kilobytes in
	/// size (see <a href="https://core.telegram.org/animated_stickers#technical-requirements"/> for animated
	/// sticker technical requirements), or a <b>WEBM</b> video with the thumbnail up to 32 kilobytes in size; see
	/// <a href="https://core.telegram.org/stickers#video-sticker-requirements"/> for video sticker technical
	/// requirements. Pass a <see cref="InputFileId"/> as a String to send a file that already exists on the
	/// Telegram servers, pass an HTTP URL as a String for Telegram to get a file from the Internet, or
	/// upload a new one using multipart/form-data. Animated and video sticker set thumbnails can't be uploaded
	/// via HTTP URL. If omitted, then the thumbnail is dropped and the first sticker is used as the thumbnail.</param>
	public async Task SetStickerSetThumbnail(string name, long userId, StickerFormat format, InputFile? thumbnail = default)
	{
		if (thumbnail == null)
			await Client.Stickers_SetStickerSetThumb(name, null);
		else
		{
			var peer = InputPeerUser(userId);
			var mimeType = MimeType(format);
			var media = await InputMediaDocument(thumbnail, mimeType: mimeType);
			var document = await UploadMediaDocument(peer, media);
			await Client.Stickers_SetStickerSetThumb(name, document);
		}
	}

	/// <summary>Use this method to set the thumbnail of a custom emoji sticker set.</summary>
	/// <param name="name">Sticker set name</param>
	/// <param name="customEmojiId">Custom emoji identifier of a <see cref="Sticker"/> from the <see cref="StickerSet"/>;
	/// pass an <see langword="null"/> to drop the thumbnail and use the first sticker as the thumbnail.</param>
	public async Task SetCustomEmojiStickerSetThumbnail(string name, string? customEmojiId = default)
	{
		await Client.Stickers_SetStickerSetThumb(name, null, customEmojiId == null ? null : long.Parse(customEmojiId));
	}

	/// <summary>Use this method to delete a sticker set that was created by the bot.</summary>
	/// <param name="name">Sticker set name</param>
	public async Task DeleteStickerSet(string name)
	{
		await Client.Stickers_DeleteStickerSet(name);
	}
	#endregion

	#region Inline mode

	/// <summary>Use this method to send answers to an inline query.</summary>
	/// <remarks>No more than <b>50</b> results per query are allowed.</remarks>
	/// <param name="inlineQueryId">Unique identifier for the answered query</param>
	/// <param name="results">An array of results for the inline query</param>
	/// <param name="cacheTime">The maximum amount of time in seconds that the result of the inline query may be cached on the server.</param>
	/// <param name="isPersonal">Pass <see langword="true"/>, if results may be cached on the server side only for the user that sent the query.
	/// By default, results may be returned to any user who sends the same query</param>
	/// <param name="nextOffset">Pass the offset that a client should send in the next query with the same text to receive more results.
	/// Pass an empty string if there are no more results or if you don't support pagination. Offset length can't exceed 64 bytes</param>
	/// <param name="button">A JSON-serialized object describing a button to be shown above inline query results</param>
	public async Task AnswerInlineQuery(string inlineQueryId, IEnumerable<InlineQueryResult> results, int cacheTime = 300, bool isPersonal = default, string? nextOffset = default, InlineQueryResultsButton? button = default)
	{
		var switch_pm = button?.StartParameter == null ? null : new InlineBotSwitchPM { text = button.Text, start_param = button.StartParameter };
		var switch_webview = button?.WebApp == null ? null : new InlineBotWebView { text = button.Text, url = button.WebApp.Url };
		await Client.Messages_SetInlineBotResults(long.Parse(inlineQueryId), await InputBotInlineResults(results), cacheTime,
			nextOffset, switch_pm, switch_webview, private_: isPersonal);
	}

	/// <summary>Use this method to set the result of an interaction with a Web App and send a corresponding message on behalf of
	/// the user to the chat from which the query originated. On success, a <see cref="SentWebAppMessage"/> object is returned.</summary>
	/// <param name="webAppQueryId">Unique identifier for the query to be answered</param>
	/// <param name="result">An object describing the message to be sent</param>
	/// <returns>The sent inline message if any</returns>
	public async Task<string?> AnswerWebAppQuery(string webAppQueryId, InlineQueryResult result)
	{
		var sent = await Client.Messages_SendWebViewResultMessage(webAppQueryId, await InputBotInlineResult(result));
		return sent.msg_id.InlineMessageId();
	}

	#endregion Inline mode

	#region Payments

	/// <summary>Use this method to send invoices.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="title">Product name, 1-32 characters</param>
	/// <param name="description">Product description, 1-255 characters</param>
	/// <param name="payload">Bot-defined invoice payload, 1-128 bytes. This will not be displayed to the user, use for your internal processes</param>
	/// <param name="providerToken">Payment provider token, obtained via <a href="https://t.me/botfather">@BotFather</a>. Pass an empty string for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="currency">Three-letter ISO 4217 currency code, see
	/// <a href="https://core.telegram.org/bots/payments#supported-currencies">more on currencies</a></param>
	/// <param name="prices">Price breakdown, a list of components (e.g. product price, tax, discount, delivery cost, delivery tax, bonus, etc.)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="maxTipAmount">The maximum accepted amount for tips in the smallest units of the currency (integer, not float/double).
	/// For example, for a maximum tip of <c>US$ 1.45</c> pass <c><paramref name="maxTipAmount"/> = 145</c>. See the <i>exp</i> parameter in
	/// <a href="https://core.telegram.org/bots/payments/currencies.json">currencies.json</a>, it shows the
	/// number of digits past the decimal point for each currency (2 for the majority of currencies).</param>
	/// <param name="suggestedTipAmounts">An array of suggested amounts of tips in the <i>smallest units</i> of the currency (integer,
	/// <b>not</b> float/double). At most 4 suggested tip amounts can be specified. The suggested tip amounts must
	/// be positive, passed in a strictly increased order and must not exceed <paramref name="maxTipAmount"/></param>
	/// <param name="startParameter">Unique deep-linking parameter. If left empty, <b>forwarded copies</b> of the sent message will have
	/// a <i>Pay</i> button, allowing multiple users to pay directly from the forwarded message, using the same
	/// invoice. If non-empty, forwarded copies of the sent message will have a <i>URL</i> button with a deep
	/// link to the bot (instead of a <i>Pay</i> button), with the value used as the start parameter</param>
	/// <param name="providerData">A JSON-serialized data about the invoice, which will be shared with the payment provider. A detailed
	/// description of required fields should be provided by the payment provide</param>
	/// <param name="photoUrl">URL of the product photo for the invoice. Can be a photo of the goods or a marketing image for a service.
	/// People like it better when they see what they are paying for</param>
	/// <param name="photoSize">Photo size</param>
	/// <param name="photoWidth">Photo width</param>
	/// <param name="photoHeight">Photo height</param>
	/// <param name="needName">Pass <see langword="true"/>, if you require the user's full name to complete the order</param>
	/// <param name="needPhoneNumber">Pass <see langword="true"/>, if you require the user's phone number to complete the order</param>
	/// <param name="needEmail">Pass <see langword="true"/>, if you require the user's email to complete the order</param>
	/// <param name="needShippingAddress">Pass <see langword="true"/>, if you require the user's shipping address to complete the order</param>
	/// <param name="sendPhoneNumberToProvider">Pass <see langword="true"/>, if user's phone number should be sent to provider</param>
	/// <param name="sendEmailToProvider">Pass <see langword="true"/>, if user's email address should be sent to provider</param>
	/// <param name="isFlexible">Pass <see langword="true"/>, if the final price depends on the shipping method</param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendInvoice(ChatId chatId, string title, string description, string payload, 
		string currency, IEnumerable<LabeledPrice> prices, string? providerToken = default, string? providerData = default, 
		int? maxTipAmount = default, IEnumerable<int>? suggestedTipAmounts = default,
		string? photoUrl = default, int? photoSize = default, int? photoWidth = default, int? photoHeight = default,
		bool needName = default, bool needPhoneNumber = default, bool needEmail = default, bool needShippingAddress = default,
		bool sendPhoneNumberToProvider = default, bool sendEmailToProvider = default, bool isFlexible = default,
		ReplyParameters? replyParameters = default, InlineKeyboardMarkup? replyMarkup = default, string? startParameter = default,
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = InputMediaInvoice(title, description, payload, providerToken, currency, prices, maxTipAmount, suggestedTipAmounts, startParameter,
			providerData, photoUrl, photoSize, photoWidth, photoHeight, needName, needPhoneNumber, needEmail, needShippingAddress,
			sendPhoneNumberToProvider, sendEmailToProvider, isFlexible);
		return await PostedMsg(Messages_SendMedia(null, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, disableNotification, protectContent, messageEffectId),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method to create a link for an invoice.</summary>
	/// <param name="title">Product name, 1-32 characters</param>
	/// <param name="description">Product description, 1-255 characters</param>
	/// <param name="payload">Bot-defined invoice payload, 1-128 bytes. This will not be displayed to the user, use for your internal processes</param>
	/// <param name="providerToken">Payment provider token, obtained via <a href="https://t.me/botfather">@BotFather</a>. Pass an empty string for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="currency">Three-letter ISO 4217 currency code, see
	/// <a href="https://core.telegram.org/bots/payments#supported-currencies">more on currencies</a></param>
	/// <param name="prices">Price breakdown, a list of components (e.g. product price, tax, discount, delivery cost, delivery tax, bonus, etc.)</param>
	/// <param name="providerData">JSON-serialized data about the invoice, which will be shared with the payment provider. A detailed
	/// description of required fields should be provided by the payment provide</param>
	/// <param name="maxTipAmount">The maximum accepted amount for tips in the smallest units of the currency (integer, not float/double).
	/// For example, for a maximum tip of <c>US$ 1.45</c> pass <c><paramref name="maxTipAmount"/> = 145</c>. See the <i>exp</i> parameter in
	/// <a href="https://core.telegram.org/bots/payments/currencies.json">currencies.json</a>, it shows the
	/// number of digits past the decimal point for each currency (2 for the majority of currencies).</param>
	/// <param name="suggestedTipAmounts">An array of suggested amounts of tips in the <i>smallest units</i> of the currency (integer,
	/// <b>not</b> float/double). At most 4 suggested tip amounts can be specified. The suggested tip amounts must
	/// be positive, passed in a strictly increased order and must not exceed <paramref name="maxTipAmount"/></param>
	/// <param name="photoUrl">URL of the product photo for the invoice. Can be a photo of the goods or a marketing image for a service.</param>
	/// <param name="photoSize">Photo size</param>
	/// <param name="photoWidth">Photo width</param>
	/// <param name="photoHeight">Photo height</param>
	/// <param name="needName">Pass <see langword="true"/>, if you require the user's full name to complete the order</param>
	/// <param name="needPhoneNumber">Pass <see langword="true"/>, if you require the user's phone number to complete the order</param>
	/// <param name="needEmail">Pass <see langword="true"/>, if you require the user's email to complete the order</param>
	/// <param name="needShippingAddress">Pass <see langword="true"/>, if you require the user's shipping address to complete the order</param>
	/// <param name="sendPhoneNumberToProvider">Pass <see langword="true"/>, if user's phone number should be sent to provider</param>
	/// <param name="sendEmailToProvider">Pass <see langword="true"/>, if user's email address should be sent to provider</param>
	/// <param name="isFlexible">Pass <see langword="true"/>, if the final price depends on the shipping method</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<string> CreateInvoiceLink(string title, string description, string payload, string? providerToken,
		string currency, IEnumerable<LabeledPrice> prices, string? providerData = default, 
		int? maxTipAmount = default, IEnumerable<int>? suggestedTipAmounts = default,
		string? photoUrl = default, int? photoSize = default, int? photoWidth = default, int? photoHeight = default,
		bool needName = default, bool needPhoneNumber = default, bool needEmail = default, bool needShippingAddress = default,
		bool sendPhoneNumberToProvider = default, bool sendEmailToProvider = default, bool isFlexible = default)
	{
		var media = InputMediaInvoice(title, description, payload, providerToken, currency, prices, maxTipAmount, suggestedTipAmounts, null,
			providerData, photoUrl, photoSize, photoWidth, photoHeight, needName, needPhoneNumber, needEmail, needShippingAddress,
			sendPhoneNumberToProvider, sendEmailToProvider, isFlexible);
		var exported = await Client.Payments_ExportInvoice(media);
		return exported.url;
	}

	/// <summary>If you sent an invoice requesting a shipping address and the parameter <c>isFlexible"</c> was specified,
	/// the Bot API will send an <see cref="Update"/> with a <see cref="ShippingQuery"/> field
	/// to the bot. Use this method to reply to shipping queries or indicate failure</summary>
	/// <param name="shippingQueryId">Unique identifier for the query to be answered</param>
	/// <param name="errorMessage">On failure, the error message in human readable form that explains why it is impossible to
	/// complete the order (e.g. "Sorry, delivery to your desired address is unavailable'). Telegram will display this message to the user</param>
	/// <param name="shippingOptions">On success, an array of available shipping options</param>
	public async Task AnswerShippingQuery(string shippingQueryId, string? errorMessage = default,
		IEnumerable<ShippingOption>? shippingOptions = default)
	{
		await Client.Messages_SetBotShippingResults(long.Parse(shippingQueryId), error: errorMessage, shipping_options:
			shippingOptions?.Select(so => new TL.ShippingOption { id = so.Id, title = so.Title, prices = so.Prices.LabeledPrices() }).ToArray());
	}

	/// <summary>Once the user has confirmed their payment and shipping details, the Bot API sends the final confirmation
	/// in the form of an <see cref="Update"/> with the field <see cref="PreCheckoutQuery"/>.
	/// Use this method to respond to it with success or failure</summary>
	/// <remarks><b>Note</b>: The Bot API must receive an answer within 10 seconds after the pre-checkout query was sent.</remarks>
	/// <param name="preCheckoutQueryId">Unique identifier for the query to be answered</param>
	/// <param name="errorMessage">Use null for success. In case of failure, the error message in
	/// human readable form that explains the reason for failure to proceed with the checkout (e.g. "Sorry,
	/// somebody just bought the last of our amazing black T-shirts while you were busy filling out your payment
	/// details. Please choose a different color or garment!"). Telegram will display this message to the user</param>
	public async Task AnswerPreCheckoutQuery(string preCheckoutQueryId, string? errorMessage = default)
	{
		await Client.Messages_SetBotPrecheckoutResults(long.Parse(preCheckoutQueryId), errorMessage, success: errorMessage == null);
	}
	#endregion Payments

	#region Telegram Passport
	/// <summary>
	/// Informs a user that some of the Telegram Passport elements they provided contains errors. The user will not be able to re-submit their Passport to you until the errors are fixed (the contents of the field for which you returned the error must change).<br/>Use this if the data submitted by the user doesn't satisfy the standards your service requires for any reason. For example, if a birthday date seems invalid, a submitted document is blurry, a scan shows evidence of tampering, etc. Supply some details in the error message to make sure the user knows how to correct the issues.
	/// </summary>
	/// <param name="userId">User identifier</param>
	/// <param name="errors">A array describing the errors</param>
	public async Task SetPassportDataErrorsAsync(long userId, IEnumerable<Telegram.Bot.Types.Passport.PassportElementError> errors)
	{
		var peer = InputPeerUser(userId);
		await Client.Users_SetSecureValueErrors(peer, errors.Select(TypesTLConverters.SecureValueError).ToArray());
	}
	#endregion Telegram Passport
	#region Games

	/// <summary>Use this method to send a game.</summary>
	/// <param name="chatId">Unique identifier for the target chat</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="gameShortName">Short name of the game, serves as the unique identifier for the game. Set up your games via
	/// <a href="https://t.me/botfather">@BotFather</a></param>
	/// <param name="disableNotification">Sends the message silently. Users will receive a notification with no sound</param>
	/// <param name="protectContent">Protects the contents of sent messages from forwarding and saving</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An <see cref="InlineKeyboardMarkup">inline keyboard</see>,
	/// <see cref="ReplyKeyboardMarkup">custom reply keyboard</see>, instructions to
	/// <see cref="ReplyKeyboardRemove">remove reply keyboard</see> or to
	/// <see cref="ForceReplyMarkup">force a reply</see> from the user</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <returns>On success, the sent <see cref="Message"/> is returned.</returns>
	public async Task<Telegram.Bot.Types.Message> SendGame(long chatId, string gameShortName, 
		ReplyParameters? replyParameters = default, InlineKeyboardMarkup? replyMarkup = default, 
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = new InputMediaGame { id = new InputGameShortName { bot_id = TL.InputUser.Self, short_name = gameShortName } };
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, disableNotification, protectContent, messageEffectId),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method to set the score of the specified user in a game.</summary>
	/// <param name="userId">User identifier</param>
	/// <param name="score">New score, must be non-negative</param>
	/// <param name="chatId">Unique identifier for the target chat</param>
	/// <param name="messageId">Identifier of the sent message</param>
	/// <param name="force">Pass <see langword="true"/>, if the high score is allowed to decrease. This can be useful when fixing mistakes
	/// or banning cheaters</param>
	/// <param name="disableEditMessage">Pass <see langword="true"/>, if the game message should not be automatically edited to include the current scoreboard</param>
	/// <returns>On success returns the edited <see cref="Message"/>. Returns an error, if the new score is not greater
	/// than the user's current score in the chat and <paramref name="force"/> is <see langword="false"/></returns>
	public async Task<Message> SetGameScore(long userId, int score, long chatId, int messageId, bool force = default, bool disableEditMessage = default)
	{
		var peer = await InputPeerChat(chatId);
		var updates = await Client.Messages_SetGameScore(peer, messageId, InputUser(userId), score, disableEditMessage != true, force);
		updates.UserOrChat(_collector);
		var editUpdate = updates.UpdateList.OfType<UpdateEditMessage>().FirstOrDefault(uem => uem.message.Peer.ID == peer.ID && uem.message.ID == messageId);
		if (editUpdate != null) return (await MakeMessage(editUpdate.message))!;
		else return await PostedMsg(Task.FromResult(updates), peer);
	}

	/// <summary>Use this method to set the score of the specified user in a game.</summary>
	/// <param name="userId">User identifier</param>
	/// <param name="score">New score, must be non-negative</param>
	/// <param name="inlineMessageId">Identifier of the inline message.</param>
	/// <param name="force">Pass <see langword="true"/>, if the high score is allowed to decrease. This can be useful when fixing mistakes
	/// or banning cheaters</param>
	/// <param name="disableEditMessage">Pass <see langword="true"/>, if the game message should not be automatically edited to include the current scoreboard</param>
	/// <returns>Returns an error, if the new score is not greater than the user's current score in the chat and
	/// <paramref name="force"/> is <see langword="false"/></returns>
	public async Task SetGameScore(long userId, int score, string inlineMessageId, bool force = default, bool disableEditMessage = default)
	{
		var id = inlineMessageId.ParseInlineMsgID();
		await Client.Messages_SetInlineGameScore(id, InputUser(userId), score, disableEditMessage != true, force);
	}

	/// <summary>Use this method to get data for high score tables. Will return the score of the specified user and several of their neighbors in a game.</summary>
	/// <remarks>This method will currently return scores for the target user, plus two of their closest neighbors on each side. 
	/// Will also return the top three users if the user and his neighbors are not among them. Please note that this behavior is subject to change.</remarks>
	/// <param name="userId">Target user id</param>
	/// <param name="chatId">Unique identifier for the target chat</param>
	/// <param name="messageId">Identifier of the sent message</param>
	/// <returns>On success, returns an Array of <see cref="GameHighScore"/> objects.</returns>
	public async Task<GameHighScore[]> GetGameHighScores(long userId, long chatId, int messageId)
	{
		var peer = await InputPeerChat(chatId);
		var highScore = await Client.Messages_GetGameHighScores(peer, messageId, InputUser(userId));
		_collector.Collect(highScore.users.Values);
		return await Task.WhenAll(highScore.scores.Select(async hs => new GameHighScore
		{ Position = hs.pos, User = await UserOrResolve(hs.user_id), Score = hs.score }));
	}

	/// <summary>Use this method to get data for high score tables. Will return the score of the specified user and several of their neighbors in a game.</summary>
	/// <remarks>This method will currently return scores for the target user, plus two of their closest neighbors on each side. 
	/// Will also return the top three users if the user and his neighbors are not among them. Please note that this behavior is subject to change.</remarks>
	/// <param name="userId">User identifier</param>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <returns>On success, returns an Array of <see cref="GameHighScore"/> objects.</returns>
	public async Task<GameHighScore[]> GetGameHighScores(long userId, string inlineMessageId)
	{
		var id = inlineMessageId.ParseInlineMsgID();
		var highScore = await Client.Messages_GetInlineGameHighScores(id, InputUser(userId));
		_collector.Collect(highScore.users.Values);
		return await Task.WhenAll(highScore.scores.Select(async hs => new GameHighScore
		{ Position = hs.pos, User = await UserOrResolve(hs.user_id), Score = hs.score }));
	}
	#endregion Games
}
