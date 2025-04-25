using TL;
using TL.Methods;
using ChatFullInfo = WTelegram.Types.ChatFullInfo;
using Message = WTelegram.Types.Message;
using MessageEntity = Telegram.Bot.Types.MessageEntity;
using Update = WTelegram.Types.Update;
using User = WTelegram.Types.User;

namespace WTelegram;

public partial class Bot
{
	const int Reactions_uniq_max = 11;

	#region Power-up methods
	/// <summary>Use this method to get a list of members in a chat (can be incomplete).</summary>
	/// <remarks>⚠️ For big chats, Telegram will likely limit the total number of members you can obtain with this method</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <param name="limit">The maximum number of member to fetch (big number might be slow to fetch, and Telegram might still restrict the maximum anyway)</param>
	/// <returns>On success, returns an Array of <see cref="ChatMember"/> objects that contains information about chat members</returns>
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
	/// <remarks>⚠️ Might be limited to 100 ids per call. Fetching other bots messages with this method will result in empty messages</remarks>
	/// <param name="chatId">The chat id or username</param>
	/// <param name="messageIds">The message IDs to fetch. You can use <c>Enumerable.Range(startMsgId, count)</c> to get a range of messages</param>
	/// <returns>List of messages that could be fetched</returns>
	public async Task<List<Message>> GetMessagesById(ChatId chatId, IEnumerable<int> messageIds)
	{
		var peer = await InputPeerChat(chatId);
		var msgs = await Client.GetMessages(peer, [.. messageIds.Select(id => (InputMessageID)id)]);
		msgs.UserOrChat(_collector);
		var messages = new List<Message>();
		foreach (var msgBase in msgs.Messages)
			if (await MakeMessage(msgBase) is { } msg)
				messages.Add(msg);
		return messages;
	}

	/// <summary>Use this method to change the bot's photo</summary>
	/// <param name="photo">New bot photo, can be an existing <see cref="InputFileId"/>, or uploaded using <see cref="InputFileStream"/>, or <see langword="null"/> to delete photo</param>
	public async Task<Telegram.Bot.Types.PhotoSize[]> SetMyPhoto(InputFile? photo)
	{
		var im = photo == null ? null : await InputMediaPhoto(photo);
		var pp = im switch
		{
			null => await Client.Photos_UpdateProfilePhoto(null),
			TL.InputMediaPhoto imp => await Client.Photos_UpdateProfilePhoto(imp.id),
			TL.InputMediaUploadedPhoto imup => await Client.Photos_UploadProfilePhoto(imup.file),
			_ => throw new WTException("Unsupported InputFile photo"),
		};
		return pp.photo.PhotoSizes()!;
	}
	#endregion Power-up methods

	#region Available methods

	/// <summary>A simple method for testing your bot's authentication token.</summary>
	/// <returns>Basic information about the bot in form of a <see cref="Types.User"/> object.</returns>
	public async Task<User> GetMe()
	{
		await InitComplete();
		var users = await Client.Users_GetUsers(TL.InputUser.Self);
		_collector.Collect(users.OfType<TL.User>());
		return User(BotId)!;
	}

	/// <summary>Use this method to close the bot instance before moving it from one local server to another. You need to delete the webhook before calling this method to ensure that the bot isn't launched again after server restart. The method will return error 429 in the first 10 minutes after the bot is launched.</summary>
	public async Task Close()
	{
		await InitComplete();
		await Client.Auth_LogOut();
	}

	/// <summary>Use this method to send text messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="text">Text of the message to be sent, 1-4096 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the message text. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="linkPreviewOptions">Link preview generation options for the message</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="entities">A list of special entities that appear in message text, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendMessage(ChatId chatId, string text, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default, LinkPreviewOptions? linkPreviewOptions = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? entities = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		var tlEntities = ApplyParse(parseMode, ref text!, entities);
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = linkPreviewOptions.InputMediaWebPage();
		if (media == null)
			return await PostedMsg(Messages_SendMessage(businessConnectionId, peer, text, Helpers.RandomLong(), reply_to,
				await MakeReplyMarkup(replyMarkup), tlEntities, messageEffectId,
				disableNotification, protectContent, allowPaidBroadcast, linkPreviewOptions?.ShowAboveText == true, linkPreviewOptions?.IsDisabled == true),
				peer, text, replyToMessage, businessConnectionId);
		else
			return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, text, Helpers.RandomLong(), reply_to,
				await MakeReplyMarkup(replyMarkup), tlEntities, messageEffectId,
				disableNotification, protectContent, allowPaidBroadcast, linkPreviewOptions?.ShowAboveText == true),
				peer, text, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to forward messages of any kind. Service messages and messages with protected content can't be forwarded.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="fromChatId">Unique identifier for the chat where the original message was sent (or channel username in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Message identifier in the chat specified in <paramref name="fromChatId"/></param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the forwarded message from forwarding and saving</param>
	/// <param name="videoStartTimestamp">New start timestamp for the forwarded video in the message</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> ForwardMessage(ChatId chatId, ChatId fromChatId, int messageId, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, int? videoStartTimestamp = default)
	{
		var peer = await InputPeerChat(chatId);
		return await PostedMsg(Client.Messages_ForwardMessages(await InputPeerChat(fromChatId), [messageId], [Helpers.RandomLong()], peer,
			top_msg_id: messageThreadId, silent: disableNotification, noforwards: protectContent, video_timestamp: videoStartTimestamp), peer);
	}

	/// <summary>Use this method to forward multiple messages of any kind. If some of the specified messages can't be found or forwarded, they are skipped. Service messages and messages with protected content can't be forwarded. Album grouping is kept for forwarded messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="fromChatId">Unique identifier for the chat where the original messages were sent (or channel username in the format <c>@channelusername</c>)</param>
	/// <param name="messageIds">A list of 1-100 identifiers of messages in the chat <paramref name="fromChatId"/> to forward. The identifiers must be specified in a strictly increasing order.</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the messages <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the forwarded messages from forwarding and saving</param>
	/// <returns>An array of sent <see cref="Message"/> is returned.</returns>
	public async Task<Message[]> ForwardMessages(ChatId chatId, ChatId fromChatId, IEnumerable<int> messageIds, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default)
	{
		var peer = await InputPeerChat(chatId);
		var random_id = Helpers.RandomLong();
		var ids = messageIds.ToArray();
		var random_ids = new long[ids.Length];
		for (int i = 0; i < ids.Length; i++) random_ids[i] = random_id + i;
		return await PostedMsgs(Client.Messages_ForwardMessages(await InputPeerChat(fromChatId), ids, random_ids, peer,
			top_msg_id: messageThreadId, silent: disableNotification, noforwards: protectContent),
			ids.Length, random_id, null);
	}

	/// <summary>Use this method to copy messages of any kind. Service messages, paid media messages, giveaway messages, giveaway winners messages, and invoice messages can't be copied. A quiz <see cref="Poll"/> can be copied only if the value of the field <em>CorrectOptionId</em> is known to the bot. The method is analogous to the method <see cref="WTelegram.Bot.ForwardMessage">ForwardMessage</see>, but the copied message doesn't have a link to the original message.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="fromChatId">Unique identifier for the chat where the original message was sent (or channel username in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Message identifier in the chat specified in <paramref name="fromChatId"/></param>
	/// <param name="caption">New caption for media, 0-1024 characters after entities parsing. If not specified, the original caption is kept</param>
	/// <param name="parseMode">Mode for parsing entities in the new caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">A list of special entities that appear in the new caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media. Ignored if a new caption isn't specified.</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <param name="videoStartTimestamp">New start timestamp for the copied video in the message</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> CopyMessage(ChatId chatId, ChatId fromChatId, int messageId, string? caption = default,
		ParseMode parseMode = default, ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default,
		bool disableNotification = default, bool protectContent = default, bool allowPaidBroadcast = default, int? videoStartTimestamp = default)
	{
		var msgs = await Client.GetMessages(await InputPeerChat(fromChatId), messageId);
		msgs.UserOrChat(_collector);
		if (msgs.Messages.FirstOrDefault() is not TL.Message msg) throw new WTException("Bad Request: message to copy not found");
		var entities = ApplyParse(parseMode, ref caption, captionEntities);
		var peer = await InputPeerChat(chatId);
		var text = caption ?? msg.message;
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var inputMedia = msg.media?.ToInputMedia();
		if (videoStartTimestamp.HasValue && inputMedia is TL.InputMediaDocument imd)
		{ imd.video_timestamp = videoStartTimestamp.Value; imd.flags |= TL.InputMediaDocument.Flags.has_video_timestamp; }
		var task = inputMedia == null
			? Messages_SendMessage(null, peer, text, Helpers.RandomLong(), reply_to,
				await MakeReplyMarkup(replyMarkup) ?? msg.reply_markup, caption != null ? entities : msg.entities,
				0, disableNotification, protectContent, allowPaidBroadcast, showCaptionAboveMedia, true)
			: Messages_SendMedia(null, peer, inputMedia, text, Helpers.RandomLong(), reply_to,
				await MakeReplyMarkup(replyMarkup) ?? msg.reply_markup, caption != null ? entities : msg.entities,
				0, disableNotification, protectContent, allowPaidBroadcast, showCaptionAboveMedia);
		var postedMsg = await PostedMsg(task, peer, text);
		return postedMsg;
	}

	/// <summary>Use this method to copy messages of any kind. If some of the specified messages can't be found or copied, they are skipped. Service messages, paid media messages, giveaway messages, giveaway winners messages, and invoice messages can't be copied. A quiz <see cref="Poll"/> can be copied only if the value of the field <em>CorrectOptionId</em> is known to the bot. The method is analogous to the method <see cref="WTelegram.Bot.ForwardMessages">ForwardMessages</see>, but the copied messages don't have a link to the original message. Album grouping is kept for copied messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="fromChatId">Unique identifier for the chat where the original messages were sent (or channel username in the format <c>@channelusername</c>)</param>
	/// <param name="messageIds">A list of 1-100 identifiers of messages in the chat <paramref name="fromChatId"/> to copy. The identifiers must be specified in a strictly increasing order.</param>
	/// <param name="removeCaption">Pass <see langword="true"/> to copy the messages without their captions</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the messages <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent messages from forwarding and saving</param>
	/// <returns>A list of sent <see cref="Message"/> is returned.</returns>
	public async Task<List<Message>> CopyMessages(ChatId chatId, ChatId fromChatId, int[] messageIds, bool removeCaption = default,
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default)
	{
		var msgs = await Client.GetMessages(await InputPeerChat(fromChatId), [.. messageIds.Select(id => (InputMessageID)id)]);
		msgs.UserOrChat(_collector);
		var peer = await InputPeerChat(chatId);
		var reply_to = await MakeReplyTo(null, messageThreadId, peer);
		var sentMsgs = new List<Message>();
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
					null, msg.entities, 0, disableNotification, protectContent, false, false, true)
				: Messages_SendMedia(null, peer, msg.media.ToInputMedia(), msg.message, random_id++, reply_to,
					null, msg.entities, 0, disableNotification, protectContent, false, msg.flags.HasFlag(TL.Message.Flags.invert_media));
			var postedMsg = await PostedMsg(task, peer);
			sentMsgs.Add(postedMsg);
		}
		if (multiMedia != null) await FlushMediaGroup();
		return sentMsgs;

		async Task FlushMediaGroup()
		{
			var postedMsgs = await PostedMsgs(Client.Messages_SendMultiMedia(peer, multiMedia?.ToArray(), reply_to,
				silent: disableNotification, noforwards: protectContent, invert_media: grouped_invert_media),
				multiMedia!.Count, multiMedia[0].random_id, null);
			sentMsgs.AddRange(postedMsgs);
			multiMedia = null;
			grouped_invert_media = false;
		}
	}

	/// <summary>Use this method to send photos.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="photo">Photo to send. Pass a FileId as String to send a photo that exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a photo from the Internet, or upload a new photo using <see cref="InputFileStream"/>. The photo must be at most 10 MB in size. The photo's width and height must not exceed 10000 in total. Width and height ratio must be at most 20. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="caption">Photo caption (may also be used when resending photos by <em>FileId</em>), 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the photo caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media</param>
	/// <param name="hasSpoiler">Pass <see langword="true"/> if the photo needs to be covered with a spoiler animation</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendPhoto(ChatId chatId, InputFile photo, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default, int messageThreadId = 0,
		IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, bool hasSpoiler = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		var entities = ApplyParse(parseMode, ref caption, captionEntities);
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = await InputMediaPhoto(photo, hasSpoiler);
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, caption, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), entities, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, showCaptionAboveMedia),
			peer, caption, replyToMessage, businessConnectionId);
	}

	private async Task<Message> SendDoc(ChatId chatId, InputFile file, string? caption, ParseMode parseMode,
		ReplyParameters? replyParameters, ReplyMarkup? replyMarkup, InputFile? thumbnail,
		int messageThreadId, IEnumerable<MessageEntity>? captionEntities, bool disableNotification,
		bool protectContent, long messageEffectId, bool allowPaidBroadcast, string? businessConnectionId,
		Action<InputMediaUploadedDocument>? prepareDoc, string? defaultFilename = null, bool showCaptionAboveMedia = false, 
		bool hasSpoiler = false, InputFile? cover = default, int? startTimestamp = default)
	{
		var entities = ApplyParse(parseMode, ref caption, captionEntities);
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var video_cover = await UploadMediaPhoto(peer, cover);
		var media = await InputMediaDocument(file, video_cover, startTimestamp, hasSpoiler, defaultFilename: defaultFilename);
		if (media is TL.InputMediaUploadedDocument doc)
		{
			prepareDoc?.Invoke(doc);
			await SetDocThumb(doc, thumbnail);
		}
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, caption, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), entities, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, showCaptionAboveMedia),
			peer, caption, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to send audio files, if you want Telegram clients to display them in the music player. Your audio must be in the .MP3 or .M4A format.</summary>
	/// <remarks>Bots can currently send audio files of up to 50 MB in size, this limit may be changed in the future.<br/>For sending voice messages, use the <see cref="WTelegram.Bot.SendVoice">SendVoice</see> method instead.</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="audio">Audio file to send. Pass a FileId as String to send an audio file that exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get an audio file from the Internet, or upload a new one using <see cref="InputFileStream"/>. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="caption">Audio caption, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the audio caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="duration">Duration of the audio in seconds</param>
	/// <param name="performer">Performer</param>
	/// <param name="title">Track name</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side. The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should not exceed 320. Ignored if the file is not uploaded using <see cref="InputFileStream"/>. Thumbnails can't be reused and can be only uploaded as a new file, so you can use <see cref="InputFileStream(Stream, string?)"/> with a specific filename. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendAudio(ChatId chatId, InputFile audio, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default,
		int duration = 0, string? performer = default, string? title = default, InputFile? thumbnail = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		return await SendDoc(chatId, audio, caption, parseMode, replyParameters, replyMarkup, thumbnail, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, allowPaidBroadcast, businessConnectionId, doc =>
			doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeAudio {
				duration = duration, performer = performer, title = title,
				flags = DocumentAttributeAudio.Flags.has_title | DocumentAttributeAudio.Flags.has_performer }]);
	}

	/// <summary>Use this method to send general files.</summary>
	/// <remarks>Bots can currently send files of any type of up to 50 MB in size, this limit may be changed in the future.</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="document">File to send. Pass a FileId as String to send a file that exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a file from the Internet, or upload a new one using <see cref="InputFileStream"/>. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="caption">Document caption (may also be used when resending documents by <em>FileId</em>), 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the document caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side. The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should not exceed 320. Ignored if the file is not uploaded using <see cref="InputFileStream"/>. Thumbnails can't be reused and can be only uploaded as a new file, so you can use <see cref="InputFileStream(Stream, string?)"/> with a specific filename. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="disableContentTypeDetection">Disables automatic server-side content type detection for files uploaded using <see cref="InputFileStream"/></param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendDocument(ChatId chatId, InputFile document, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default, InputFile? thumbnail = default,
		int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default, bool disableContentTypeDetection = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		return await SendDoc(chatId, document, caption, parseMode, replyParameters, replyMarkup, thumbnail, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, allowPaidBroadcast, businessConnectionId, doc =>
			{ if (disableContentTypeDetection) doc.flags |= InputMediaUploadedDocument.Flags.force_file; }, "document");
	}

	/// <summary>Use this method to send video files, Telegram clients support MPEG4 videos (other formats may be sent as <see cref="Document"/>).</summary>
	/// <remarks>Bots can currently send video files of up to 50 MB in size, this limit may be changed in the future.</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="video">Video to send. Pass a FileId as String to send a video that exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a video from the Internet, or upload a new video using <see cref="InputFileStream"/>. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="caption">Video caption (may also be used when resending videos by <em>FileId</em>), 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the video caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="duration">Duration of sent video in seconds</param>
	/// <param name="width">Video width</param>
	/// <param name="height">Video height</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side. The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should not exceed 320. Ignored if the file is not uploaded using <see cref="InputFileStream"/>. Thumbnails can't be reused and can be only uploaded as a new file, so you can use <see cref="InputFileStream(Stream, string?)"/> with a specific filename. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media</param>
	/// <param name="hasSpoiler">Pass <see langword="true"/> if the video needs to be covered with a spoiler animation</param>
	/// <param name="supportsStreaming">Pass <see langword="true"/> if the uploaded video is suitable for streaming</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <param name="cover">Cover for the video in the message. Pass a FileId to send a file that exists on the Telegram servers (recommended), pass an HTTP URL for Telegram to get a file from the Internet, or use <see cref="InputFileStream(Stream, string?)"/> with a specific filename. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="startTimestamp">Start timestamp for the video in the message</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendVideo(ChatId chatId, InputFile video, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default,
		int duration = 0, int width = 0, int height = 0, InputFile? thumbnail = default, int messageThreadId = 0,
		IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, bool hasSpoiler = default,
		bool supportsStreaming = default, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0,
		string? businessConnectionId = default, bool allowPaidBroadcast = default, InputFile? cover = default, int? startTimestamp = default)
	{
		return await SendDoc(chatId, video, caption, parseMode, replyParameters, replyMarkup, thumbnail, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, allowPaidBroadcast, businessConnectionId, doc =>
			doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeVideo {
				duration = duration, h = height, w = width,
				flags = supportsStreaming ? DocumentAttributeVideo.Flags.supports_streaming : 0 }],
			default, showCaptionAboveMedia, hasSpoiler, cover, startTimestamp);
	}

	/// <summary>Use this method to send animation files (GIF or H.264/MPEG-4 AVC video without sound).</summary>
	/// <remarks>Bots can currently send animation files of up to 50 MB in size, this limit may be changed in the future.</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="animation">Animation to send. Pass a FileId as String to send an animation that exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get an animation from the Internet, or upload a new animation using <see cref="InputFileStream"/>. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="caption">Animation caption (may also be used when resending animation by <em>FileId</em>), 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the animation caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="duration">Duration of sent animation in seconds</param>
	/// <param name="width">Animation width</param>
	/// <param name="height">Animation height</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side. The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should not exceed 320. Ignored if the file is not uploaded using <see cref="InputFileStream"/>. Thumbnails can't be reused and can be only uploaded as a new file, so you can use <see cref="InputFileStream(Stream, string?)"/> with a specific filename. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media</param>
	/// <param name="hasSpoiler">Pass <see langword="true"/> if the animation needs to be covered with a spoiler animation</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendAnimation(ChatId chatId, InputFile animation, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default,
		int duration = 0, int width = 0, int height = 0, InputFile? thumbnail = default, int messageThreadId = 0,
		IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, bool hasSpoiler = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		return await SendDoc(chatId, animation, caption, parseMode, replyParameters, replyMarkup, thumbnail, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, allowPaidBroadcast, businessConnectionId, doc =>
			{
				doc.attributes ??= [];
				if (doc.mime_type == "video/mp4")
					doc.attributes = [.. doc.attributes, new DocumentAttributeVideo { duration = duration, w = width, h = height }];
				else if (width > 0 && height > 0)
				{
					if (doc.mime_type?.StartsWith("image/") != true) doc.mime_type = "image/gif";
					doc.attributes = [.. doc.attributes, new DocumentAttributeImageSize { w = width, h = height }];
				}
			}, "animation", showCaptionAboveMedia, hasSpoiler);
	}

	/// <summary>Use this method to send audio files, if you want Telegram clients to display the file as a playable voice message. For this to work, your audio must be in an .OGG file encoded with OPUS, or in .MP3 format, or in .M4A format (other formats may be sent as <see cref="Audio"/> or <see cref="Document"/>).</summary>
	/// <remarks>Bots can currently send voice messages of up to 50 MB in size, this limit may be changed in the future.</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="voice">Audio file to send. Pass a FileId as String to send a file that exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a file from the Internet, or upload a new one using <see cref="InputFileStream"/>. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="caption">Voice message caption, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the voice message caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="duration">Duration of the voice message in seconds</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendVoice(ChatId chatId, InputFile voice, string? caption = default, ParseMode parseMode = default,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default,
		int duration = 0, int messageThreadId = 0, IEnumerable<MessageEntity>? captionEntities = default,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		return await SendDoc(chatId, voice, caption, parseMode, replyParameters, replyMarkup, null, messageThreadId,
			captionEntities, disableNotification, protectContent, messageEffectId, allowPaidBroadcast, businessConnectionId, doc =>
			{
				doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeAudio {
					duration = duration, flags = DocumentAttributeAudio.Flags.voice }];
				if (doc.mime_type is not "audio/ogg" and not "audio/mpeg" and not "audio/mp4") doc.mime_type = "audio/ogg";
			});
	}

	/// <summary>As of <a href="https://telegram.org/blog/video-messages-and-telescope">v.4.0</a>, Telegram clients support rounded square MPEG4 videos of up to 1 minute long. Use this method to send video messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="videoNote">Video note to send. Pass a FileId as String to send a video note that exists on the Telegram servers (recommended) or upload a new video using <see cref="InputFileStream"/>. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a>. Sending video notes by a URL is currently unsupported</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="duration">Duration of sent video in seconds</param>
	/// <param name="length">Video width and height, i.e. diameter of the video message</param>
	/// <param name="thumbnail">Thumbnail of the file sent; can be ignored if thumbnail generation for the file is supported server-side. The thumbnail should be in JPEG format and less than 200 kB in size. A thumbnail's width and height should not exceed 320. Ignored if the file is not uploaded using <see cref="InputFileStream"/>. Thumbnails can't be reused and can be only uploaded as a new file, so you can use <see cref="InputFileStream(Stream, string?)"/> with a specific filename. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendVideoNote(ChatId chatId, InputFile videoNote,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default,
		int duration = 0, int? length = default, InputFile? thumbnail = default, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		return await SendDoc(chatId, videoNote, default, default, replyParameters, replyMarkup, thumbnail, messageThreadId,
			default, disableNotification, protectContent, messageEffectId, allowPaidBroadcast, businessConnectionId, doc =>
			{
				doc.flags |= InputMediaUploadedDocument.Flags.nosound_video;
				doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeVideo {
					flags = DocumentAttributeVideo.Flags.round_message, duration = duration, w = length ?? 384, h = length ?? 384 }];
			});
	}

	/// <summary>Use this method to send paid media.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>). If the chat is a channel, all Telegram Star proceeds from this media will be credited to the chat's balance. Otherwise, they will be credited to the bot's balance.</param>
	/// <param name="starCount">The number of Telegram Stars that must be paid to buy access to the media; 1-10000</param>
	/// <param name="media">A array describing the media to be sent; up to 10 items</param>
	/// <param name="caption">Media caption, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the media caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="payload">Bot-defined paid media payload, 0-128 bytes. This will not be displayed to the user, use it for your internal processes.</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendPaidMedia(ChatId chatId, int starCount, IEnumerable<InputPaidMedia> media, string? caption = default,
		ParseMode parseMode = default, ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default,
		string? payload = default, IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default,
		bool disableNotification = default, bool protectContent = default, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		var entities = ApplyParse(parseMode, ref caption, captionEntities);
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, 0, peer);
		List<TL.InputMedia> multimedia = [];
		foreach (var ipm in media)
		{
			TL.InputMedia tlMedia;
			if (ipm is not InputPaidMediaVideo ipmv)
				tlMedia = await InputMediaPhoto(ipm.Media);
			else if ((tlMedia = await InputMediaDocument(ipm.Media, await UploadMediaPhoto(peer, ipmv.Cover), ipmv.StartTimestamp)) is TL.InputMediaUploadedDocument doc)
				doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeVideo {
					duration = ipmv.Duration, h = ipmv.Height, w = ipmv.Width,
					flags = ipmv.SupportsStreaming == true ? DocumentAttributeVideo.Flags.supports_streaming : 0 }];
			if (ipm.Media.FileType != FileType.Id) // External or Uploaded
				tlMedia = (await Client.Messages_UploadMedia(peer, tlMedia)).ToInputMedia();
			multimedia.Add(tlMedia);
		}
		var impm = new InputMediaPaidMedia
		{
			flags = payload != null ? InputMediaPaidMedia.Flags.has_payload : 0,
			stars_amount = starCount,
			extended_media = [.. multimedia],
			payload = payload
		};
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, impm, caption, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), entities, 0, disableNotification, protectContent, allowPaidBroadcast, showCaptionAboveMedia),
			peer, caption, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to send a group of photos, videos, documents or audios as an album. Documents and audio files can be only grouped in an album with messages of the same type.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="media">An array describing messages to be sent, must include 2-10 items</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends messages <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent messages from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>An array of <see cref="Message">Messages</see> that were sent is returned.</returns>
	public async Task<Message[]> SendMediaGroup(ChatId chatId, IEnumerable<IAlbumInputMedia> media,
		ReplyParameters? replyParameters = default, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		List<InputSingleMedia> multimedia = [];
		var random_id = Helpers.RandomLong();
		bool invert_media = false;
		foreach (var aim in media)
		{
			var medium = (InputMedia)aim;
			invert_media |= medium switch { Telegram.Bot.Types.InputMediaPhoto imp => imp.ShowCaptionAboveMedia, InputMediaVideo imv => imv.ShowCaptionAboveMedia, InputMediaAnimation ima => ima.ShowCaptionAboveMedia, _ => false };
			var ism = await InputSingleMedia(peer, medium);
			ism.random_id = random_id + multimedia.Count;
			if (medium.Media.FileType != FileType.Id) // External or Uploaded
				ism.media = (await Client.Messages_UploadMedia(peer, ism.media)).ToInputMedia();
			multimedia.Add(ism);
		}
		return await PostedMsgs(Messages_SendMultiMedia(businessConnectionId, peer, [.. multimedia], reply_to,
			messageEffectId, disableNotification, protectContent, allowPaidBroadcast, invert_media),
			multimedia.Count, random_id, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to send point on the map.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="latitude">Latitude of the location</param>
	/// <param name="longitude">Longitude of the location</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="horizontalAccuracy">The radius of uncertainty for the location, measured in meters; 0-1500</param>
	/// <param name="livePeriod">Period in seconds during which the location will be updated (see <a href="https://telegram.org/blog/live-locations">Live Locations</a>, should be between 60 and 86400, or 0x7FFFFFFF for live locations that can be edited indefinitely.</param>
	/// <param name="heading">For live locations, a direction in which the user is moving, in degrees. Must be between 1 and 360 if specified.</param>
	/// <param name="proximityAlertRadius">For live locations, a maximum distance for proximity alerts about approaching another chat member, in meters. Must be between 1 and 100000 if specified.</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendLocation(ChatId chatId, double latitude, double longitude,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default, int horizontalAccuracy = 0,
		int livePeriod = 0, int heading = 0, int proximityAlertRadius = 0, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		TL.InputMedia media = livePeriod > 0 ? MakeGeoLive(latitude, longitude, horizontalAccuracy, heading, proximityAlertRadius, livePeriod)
			: new TL.InputMediaGeoPoint { geo_point = new InputGeoPoint { lat = latitude, lon = longitude } };
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, false),
			peer, null, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to send information about a venue.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="latitude">Latitude of the venue</param>
	/// <param name="longitude">Longitude of the venue</param>
	/// <param name="title">Name of the venue</param>
	/// <param name="address">Address of the venue</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="foursquareId">Foursquare identifier of the venue</param>
	/// <param name="foursquareType">Foursquare type of the venue, if known. (For example, “arts_entertainment/default”, “arts_entertainment/aquarium” or “food/icecream”.)</param>
	/// <param name="googlePlaceId">Google Places identifier of the venue</param>
	/// <param name="googlePlaceType">Google Places type of the venue. (See <a href="https://developers.google.com/places/web-service/supported_types">supported types</a>.)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendVenue(ChatId chatId, double latitude, double longitude, string title, string address,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default, string? foursquareId = default,
		string? foursquareType = default, string? googlePlaceId = default, string? googlePlaceType = default, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
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
			await MakeReplyMarkup(replyMarkup), null, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, false),
			peer, null, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to send phone contacts.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="phoneNumber">Contact's phone number</param>
	/// <param name="firstName">Contact's first name</param>
	/// <param name="lastName">Contact's last name</param>
	/// <param name="vcard">Additional data about the contact in the form of a <a href="https://en.wikipedia.org/wiki/VCard">vCard</a>, 0-2048 bytes</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendContact(ChatId chatId, string phoneNumber, string firstName, string? lastName = default,
		string? vcard = default, ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default,
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0,
		string? businessConnectionId = default, bool allowPaidBroadcast = default)
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
			await MakeReplyMarkup(replyMarkup), null, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, false),
			peer, null, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to send a native poll.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="question">Poll question, 1-300 characters</param>
	/// <param name="options">A list of 2-10 answer options</param>
	/// <param name="isAnonymous"><see langword="true"/>, if the poll needs to be anonymous, defaults to <see langword="true"/></param>
	/// <param name="type">Poll type, <see cref="PollType.Quiz">Quiz</see> or <see cref="PollType.Regular">Regular</see>, defaults to <see cref="PollType.Regular">Regular</see></param>
	/// <param name="allowsMultipleAnswers"><see langword="true"/>, if the poll allows multiple answers, ignored for polls in quiz mode, defaults to <see langword="false"/></param>
	/// <param name="correctOptionId">0-based identifier of the correct answer option, required for polls in quiz mode</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="explanation">Text that is shown when a user chooses an incorrect answer or taps on the lamp icon in a quiz-style poll, 0-200 characters with at most 2 line feeds after entities parsing</param>
	/// <param name="explanationParseMode">Mode for parsing entities in the explanation. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="explanationEntities">A list of special entities that appear in the poll explanation. It can be specified instead of <paramref name="explanationParseMode"/></param>
	/// <param name="questionParseMode">Mode for parsing entities in the question. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details. Currently, only custom emoji entities are allowed</param>
	/// <param name="questionEntities">A list of special entities that appear in the poll question. It can be specified instead of <paramref name="questionParseMode"/></param>
	/// <param name="openPeriod">Amount of time in seconds the poll will be active after creation, 5-600. Can't be used together with <paramref name="closeDate"/>.</param>
	/// <param name="closeDate">Point in time when the poll will be automatically closed. Must be at least 5 and no more than 600 seconds in the future. Can't be used together with <paramref name="openPeriod"/>.</param>
	/// <param name="isClosed">Pass <see langword="true"/> if the poll needs to be immediately closed. This can be useful for poll preview.</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendPoll(ChatId chatId, string question, IEnumerable<InputPollOption> options, bool isAnonymous = true,
		PollType type = PollType.Regular, bool allowsMultipleAnswers = default, int? correctOptionId = default,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default, string? explanation = default,
		ParseMode explanationParseMode = default, IEnumerable<MessageEntity>? explanationEntities = default, ParseMode questionParseMode = default,
		IEnumerable<MessageEntity>? questionEntities = default, int? openPeriod = default, DateTime? closeDate = default,
		bool isClosed = default, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		var exEntities = ApplyParse(explanationParseMode, ref explanation, explanationEntities);
		var quEntities = ApplyParse(questionParseMode, ref question!, questionEntities);
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
				question = new TextWithEntities() { text = question, entities = quEntities },
				answers = [.. options.Select(MakePollAnswer)],
				close_period = openPeriod.GetValueOrDefault(),
				close_date = closeDate.GetValueOrDefault(),
			},
			correct_answers = correctOptionId == null ? null : [[(byte)correctOptionId]],
			solution = explanation,
			solution_entities = exEntities,
			flags = (explanation != null ? InputMediaPoll.Flags.has_solution : 0)
				| (correctOptionId >= 0 ? InputMediaPoll.Flags.has_correct_answers : 0)
		};
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, false),
			peer, null, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to send an animated emoji that will display a random value.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="emoji">Emoji on which the dice throw animation is based. Currently, must be one of “🎲”, “🎯”, “🏀”, “⚽”, “🎳”, or “🎰”. Dice can have values 1-6 for “🎲”, “🎯” and “🎳”, values 1-5 for “🏀” and “⚽”, and values 1-64 for “🎰”. Defaults to “🎲”</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendDice(ChatId chatId, string emoji = DiceEmoji.Dice,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = new InputMediaDice { emoticon = emoji };
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, false),
			peer, null, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method when you need to tell the user that something is happening on the bot's side. The status is set for 5 seconds or less (when a message arrives from your bot, Telegram clients clear its typing status).<br/>We only recommend using this method when a response from the bot will take a <b>noticeable</b> amount of time to arrive.</summary>
	/// <remarks>Example: The <a href="https://t.me/imagebot">ImageBot</a> needs some time to process a request and upload the image. Instead of sending a text message along the lines of “Retrieving image, please wait…”, the bot may use <see cref="WTelegram.Bot.SendChatAction">SendChatAction</see> with <paramref name="action"/> = <em>UploadPhoto</em>. The user will see a “sending photo” status for the bot.</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="action">Type of action to broadcast. Choose one, depending on what the user is about to receive: <em>typing</em> for <see cref="WTelegram.Bot.SendMessage">text messages</see>, <em>UploadPhoto</em> for <see cref="WTelegram.Bot.SendPhoto">photos</see>, <em>RecordVideo</em> or <em>UploadVideo</em> for <see cref="WTelegram.Bot.SendVideo">videos</see>, <em>RecordVoice</em> or <em>UploadVoice</em> for <see cref="WTelegram.Bot.SendVoice">voice notes</see>, <em>UploadDocument</em> for <see cref="WTelegram.Bot.SendDocument">general files</see>, <em>ChooseSticker</em> for <see cref="WTelegram.Bot.SendSticker">stickers</see>, <em>FindLocation</em> for <see cref="WTelegram.Bot.SendLocation">location data</see>, <em>RecordVideoNote</em> or <em>UploadVideoNote</em> for <see cref="WTelegram.Bot.SendVideoNote">video notes</see>.</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread; for supergroups only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the action will be sent</param>
	public async Task SendChatAction(ChatId chatId, ChatAction action, int messageThreadId = 0, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		if (businessConnectionId is null)
			await Client.Messages_SetTyping(peer, action.ChatAction(), messageThreadId);
		else
			await Client.InvokeWithBusinessConnection(businessConnectionId,
				new Messages_SetTyping { peer = peer, action = action.ChatAction(), top_msg_id = messageThreadId,
					flags = messageThreadId != 0 ? Messages_SetTyping.Flags.has_top_msg_id : 0 });
	}

	/// <summary>Use this method to change the chosen reactions on a message. Service messages of some types can't be reacted to. Automatically forwarded messages from a channel to its discussion group have the same available reactions as messages in the channel. Bots can't use paid reactions.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the target message. If the message belongs to a media group, the reaction is set to the first non-deleted message in the group instead.</param>
	/// <param name="reaction">A list of reaction types to set on the message. Currently, as non-premium users, bots can set up to one reaction per message. A custom emoji reaction can be used if it is either already present on the message or explicitly allowed by chat administrators. Paid reactions can't be used by bots.</param>
	/// <param name="isBig">Pass <see langword="true"/> to set the reaction with a big animation</param>
	public async Task SetMessageReaction(ChatId chatId, int messageId, IEnumerable<ReactionType>? reaction, bool isBig = default)
	{
		var peer = await InputPeerChat(chatId);
		reaction ??= [];
		var updates = await Client.Messages_SendReaction(peer, messageId, [.. reaction.Select(TypesTLConverters.Reaction)], big: isBig);
	}

	/// <summary>Use this method to get a list of profile pictures for a user.</summary>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="offset">Sequential number of the first photo to be returned. By default, all photos are returned.</param>
	/// <param name="limit">Limits the number of photos to be retrieved. Values between 1-100 are accepted. Defaults to 100.</param>
	/// <returns>A <see cref="UserProfilePhotos"/> object.</returns>
	public async Task<UserProfilePhotos> GetUserProfilePhotos(long userId, int offset = 0, int limit = 100)
	{
		await InitComplete();
		var inputUser = InputUser(userId);
		var photos = await Client.Photos_GetUserPhotos(inputUser, offset, limit: limit);
		return new UserProfilePhotos
		{
			TotalCount = (photos as Photos_PhotosSlice)?.count ?? photos.photos.Length,
			Photos = [.. photos.photos.Select(pb => pb.PhotoSizes()!)]
		};
	}

	/// <summary>Changes the emoji status for a given user that previously allowed the bot to manage their emoji status via the Mini App method <a href="https://core.telegram.org/bots/webapps#initializing-mini-apps">requestEmojiStatusAccess</a>.</summary>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="emojiStatusCustomEmojiId">Custom emoji identifier of the emoji status to set. Pass an empty string to remove the status.</param>
	/// <param name="emojiStatusExpirationDate">Expiration date of the emoji status, if any</param>
	public async Task SetUserEmojiStatus(long userId, string? emojiStatusCustomEmojiId = default, DateTime? emojiStatusExpirationDate = default)
	{
		await InitComplete();
		var inputUser = InputUser(userId);
		EmojiStatusBase? emojiStatus = null;
		if (!string.IsNullOrEmpty(emojiStatusCustomEmojiId))
			emojiStatus = new EmojiStatus
			{
				document_id = long.Parse(emojiStatusCustomEmojiId),
				until = emojiStatusExpirationDate.GetValueOrDefault(),
				flags = emojiStatusExpirationDate.HasValue ? EmojiStatus.Flags.has_until : 0
			};
		await Client.Bots_UpdateUserEmojiStatus(inputUser, emojiStatus);
	}

	/// <summary>Use this method to get basic information about a file and prepare it for downloading. For the moment, bots can download files of up to 20MB in size.</summary>
	/// <param name="fileId">File identifier to get information about</param>
	/// <returns>A <see cref="TGFile"/> object is returned. The file can then be downloaded via <see cref="WTelegram.Bot.DownloadFile">DownloadFile</see>, where <c>&lt;FilePath&gt;</c> is taken from the response. It is guaranteed that the link will be valid for at least 1 hour. When the link expires, a new one can be requested by calling <see cref="WTelegram.Bot.GetFile">GetFile</see> again.<br/><b>Note:</b> This function may not preserve the original file name and MIME type. You should save the file's MIME type and name (if available) when the File object is received.</returns>
	public Task<TGFile> GetFile(string fileId) =>
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

	/// <summary>Use this method to get basic info about a file download it. For the moment, bots can download files of up to 20MB in size.</summary>
	/// <param name="fileId">File identifier to get info about</param>
	/// <param name="destination">Destination stream to write file to</param>
	/// <param name="cancellationToken">If you need to abort the download</param>
	/// <returns>On success, a <see cref="File"/> object is returned.</returns>
	public async Task<TGFile> GetInfoAndDownloadFile(string fileId, Stream destination, CancellationToken cancellationToken = default)
	{
		var (file, location, dc_id) = fileId.ParseFileId(true);
		await InitComplete();
		await Client.DownloadFileAsync(location, destination, dc_id, file.FileSize ?? 0, (t, s) => cancellationToken.ThrowIfCancellationRequested());
		return file;
	}

	/// <summary>Use this method to ban a user in a group, a supergroup or a channel. In the case of supergroups and channels, the user will not be able to return to the chat on their own using invite links, etc., unless <see cref="WTelegram.Bot.UnbanChatMember">unbanned</see> first. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target group or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="untilDate">Date when the user will be unbanned, in UTC. If user is banned for more than 366 days or less than 30 seconds from the current time they are considered to be banned forever. Applied for supergroups and channels only.</param>
	/// <param name="revokeMessages">Pass <see langword="true"/> to delete all messages from the chat for the user that is being removed. If <see langword="false"/>, the user will be able to see messages in the group that were sent before the user was removed. Always <see langword="true"/> for supergroups and channels.</param>
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

	/// <summary>Use this method to unban a previously banned user in a supergroup or channel. The user will <b>not</b> return to the group or channel automatically, but will be able to join via link, etc. The bot must be an administrator for this to work. By default, this method guarantees that after the call the user is not a member of the chat, but will be able to join it. So if the user is a member of the chat they will also be <b>removed</b> from the chat. If you don't want this, use the parameter <paramref name="onlyIfBanned"/>.</summary>
	/// <param name="chatId">Unique identifier for the target group or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
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

	/// <summary>Use this method to restrict a user in a supergroup. The bot must be an administrator in the supergroup for this to work and must have the appropriate administrator rights. Pass <em>True</em> for all permissions to lift restrictions from a user.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="permissions">An object for new user permissions</param>
	/// <param name="untilDate">Date when restrictions will be lifted for the user, in UTC. If user is restricted for more than 366 days or less than 30 seconds from the current time, they are considered to be restricted forever</param>
	public async Task RestrictChatMember(ChatId chatId, long userId, ChatPermissions permissions, DateTime? untilDate = default)
	{
		var channel = await InputChannel(chatId);
		var user = InputPeerUser(userId);
		await Client.Channels_EditBanned(channel, user, permissions.ToChatBannedRights(untilDate));
	}

	/// <summary>Use this method to promote or demote a user in a supergroup or a channel. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights. Pass <c><see langword="null"/></c> rights to demote a user.</summary>
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

	/// <summary>Use this method to ban/unban a channel chat in a supergroup or a channel. Until the chat is unbanned, the owner of the banned chat won't be able to send messages on behalf of <b>any of their channels</b>. The bot must be an administrator in the supergroup or channel for this to work and must have the appropriate administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="senderChatId">Unique identifier of the target sender chat</param>
	/// <param name="ban">whether to ban or unban</param>
	public async Task BanUnbanChatSenderChat(ChatId chatId, long senderChatId, bool ban = true)
	{
		var channel = await InputChannel(chatId);
		var senderChat = await InputPeerChat(senderChatId);
		await Client.Channels_EditBanned(channel, senderChat, new ChatBannedRights { flags = ban ? ChatBannedRights.Flags.view_messages : 0 });
	}

	/// <summary>Use this method to set default chat permissions for all members. The bot must be an administrator in the group or a supergroup for this to work and must have the <em>CanRestrictMembers</em> administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="permissions">An object for new default chat permissions</param>
	public async Task SetChatPermissions(ChatId chatId, ChatPermissions permissions)
	{
		var peer = await InputPeerChat(chatId);
		try
		{
			await Client.Messages_EditChatDefaultBannedRights(peer, permissions.ToChatBannedRights());
		}
		catch (RpcException ex) when (ex.Message.EndsWith("_NOT_MODIFIED")) { }
	}

	/// <summary>Use this method to generate a new primary invite link for a chat; any previously generated primary link is revoked. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights.</summary>
	/// <remarks>Note: Each administrator in a chat generates their own invite links. Bots can't use invite links generated by other administrators. If you want your bot to work with invite links, it will need to generate its own link using <see cref="WTelegram.Bot.ExportChatInviteLink">ExportChatInviteLink</see> or by calling the <see cref="WTelegram.Bot.GetChat">GetChat</see> method. If your bot needs to generate a new primary invite link replacing its previous one, use <see cref="WTelegram.Bot.ExportChatInviteLink">ExportChatInviteLink</see> again.</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <returns>The new invite link as <em>String</em> on success.</returns>
	public async Task<string> ExportChatInviteLink(ChatId chatId)
	{
		var peer = await InputPeerChat(chatId);
		var exported = (ChatInviteExported)await Client.Messages_ExportChatInvite(peer, legacy_revoke_permanent: true);
		return exported.link;
	}

	/// <summary>Use this method to create an additional invite link for a chat. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights. The link can be revoked using the method <see cref="WTelegram.Bot.RevokeChatInviteLink">RevokeChatInviteLink</see>.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="name">Invite link name; 0-32 characters</param>
	/// <param name="expireDate">Point in time when the link will expire</param>
	/// <param name="memberLimit">The maximum number of users that can be members of the chat simultaneously after joining the chat via this invite link; 1-99999</param>
	/// <param name="createsJoinRequest"><see langword="true"/>, if users joining the chat via the link need to be approved by chat administrators. If <see langword="true"/>, <paramref name="memberLimit"/> can't be specified</param>
	/// <returns>The new invite link as <see cref="ChatInviteLink"/> object.</returns>
	public async Task<ChatInviteLink> CreateChatInviteLink(ChatId chatId, string? name = default, DateTime? expireDate = default,
		int? memberLimit = default, bool createsJoinRequest = default)
	{
		var peer = await InputPeerChat(chatId);
		ExportedChatInvite exported = await Client.Messages_ExportChatInvite(peer, expireDate, memberLimit, name, request_needed: createsJoinRequest);
		return (await MakeChatInviteLink(exported))!;
	}

	/// <summary>Use this method to edit a non-primary invite link created by the bot. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="inviteLink">The invite link to edit</param>
	/// <param name="name">Invite link name; 0-32 characters</param>
	/// <param name="expireDate">Point in time when the link will expire</param>
	/// <param name="memberLimit">The maximum number of users that can be members of the chat simultaneously after joining the chat via this invite link; 1-99999</param>
	/// <param name="createsJoinRequest"><see langword="true"/>, if users joining the chat via the link need to be approved by chat administrators. If <see langword="true"/>, <paramref name="memberLimit"/> can't be specified</param>
	/// <returns>The edited invite link as a <see cref="ChatInviteLink"/> object.</returns>
	public async Task<ChatInviteLink> EditChatInviteLink(ChatId chatId, string inviteLink, string? name = default,
		DateTime? expireDate = default, int? memberLimit = default, bool createsJoinRequest = default)
	{
		var peer = await InputPeerChat(chatId);
		var result = await Client.Messages_EditExportedChatInvite(peer, inviteLink, expireDate, memberLimit, title: name, request_needed: createsJoinRequest);
		return (await MakeChatInviteLink(result.Invite))!;
	}

	/// <summary>Use this method to create a <a href="https://telegram.org/blog/superchannels-star-reactions-subscriptions#star-subscriptions">subscription invite link</a> for a channel chat. The bot must have the <em>CanInviteUsers</em> administrator rights. The link can be edited using the method <see cref="WTelegram.Bot.EditChatSubscriptionInviteLink">EditChatSubscriptionInviteLink</see> or revoked using the method <see cref="WTelegram.Bot.RevokeChatInviteLink">RevokeChatInviteLink</see>.</summary>
	/// <param name="chatId">Unique identifier for the target channel chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="subscriptionPeriod">The number of seconds the subscription will be active for before the next payment. Currently, it must always be 2592000 (30 days).</param>
	/// <param name="subscriptionPrice">The amount of Telegram Stars a user must pay initially and after each subsequent subscription period to be a member of the chat; 1-10000</param>
	/// <param name="name">Invite link name; 0-32 characters</param>
	/// <returns>The new invite link as a <see cref="ChatInviteLink"/> object.</returns>
	public async Task<ChatInviteLink> CreateChatSubscriptionInviteLink(ChatId chatId, int subscriptionPeriod, int subscriptionPrice,
		string? name = default)
	{
		var peer = await InputPeerChat(chatId);
		ExportedChatInvite exported = await Client.Messages_ExportChatInvite(peer, null, null, name, new() { amount = subscriptionPrice, period = subscriptionPeriod });
		return (await MakeChatInviteLink(exported))!;
	}

	/// <summary>Use this method to edit a subscription invite link created by the bot. The bot must have the <em>CanInviteUsers</em> administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="inviteLink">The invite link to edit</param>
	/// <param name="name">Invite link name; 0-32 characters</param>
	/// <returns>The edited invite link as a <see cref="ChatInviteLink"/> object.</returns>
	public async Task<ChatInviteLink> EditChatSubscriptionInviteLink(ChatId chatId, string inviteLink, string? name = default)
	{
		var peer = await InputPeerChat(chatId);
		var result = await Client.Messages_EditExportedChatInvite(peer, inviteLink, title: name);
		return (await MakeChatInviteLink(result.Invite))!;
	}

	/// <summary>Use this method to revoke an invite link created by the bot. If the primary link is revoked, a new link is automatically generated. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights.</summary>
	/// <param name="chatId">Unique identifier of the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="inviteLink">The invite link to revoke</param>
	/// <returns>The revoked invite link as <see cref="ChatInviteLink"/> object.</returns>
	public async Task<ChatInviteLink> RevokeChatInviteLink(ChatId chatId, string inviteLink)
	{
		var peer = await InputPeerChat(chatId);
		var result = await Client.Messages_EditExportedChatInvite(peer, inviteLink, revoked: true);
		return (await MakeChatInviteLink(result.Invite))!;
	}

	/// <summary>Use this method to approve/decline a chat join request. The bot must be an administrator in the chat for this to work and must have the <see cref="ChatPermissions.CanInviteUsers"/> administrator right.</summary>
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

	/// <summary>Use this method to set (or delete) a new profile photo for the chat. Photos can't be changed for private chats. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="photo">New chat photo, uploaded using <see cref="InputFileStream"/>, or <see langword="null"/> to delete photo</param>
	public async Task SetChatPhoto(ChatId chatId, InputFileStream? photo)
	{
		var peer = await InputPeerChat(chatId);
		var inputPhoto = photo == null ? null : await InputChatPhoto(photo);
		await Client.EditChatPhoto(peer, inputPhoto);
	}

	/// <summary>Use this method to change the title of a chat. Titles can't be changed for private chats. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="title">New chat title, 1-128 characters</param>
	public async Task SetChatTitle(ChatId chatId, string title)
	{
		var peer = await InputPeerChat(chatId);
		try
		{
			await Client.EditChatTitle(peer, title);
		}
		catch (RpcException ex) when (ex.Message.EndsWith("_NOT_MODIFIED")) { }
	}

	/// <summary>Use this method to change the description of a group, a supergroup or a channel. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="description">New chat description, 0-255 characters</param>
	public async Task SetChatDescription(ChatId chatId, string? description = default)
	{
		var peer = await InputPeerChat(chatId);
		await Client.Messages_EditChatAbout(peer, description);
	}

	/// <summary>Use this method to add/remove a message in the list of pinned messages in a chat. If the chat is not a private chat, the bot must be an administrator in the chat for this to work and must have the '<see cref="ChatMemberAdministrator.CanPinMessages"/>' admin right in a supergroup or '<see cref="ChatMemberAdministrator.CanEditMessages"/>' administrator right in a channel</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of a message to pin/unpin. To unpin the most recent pinned message (by sending date), pass 0.</param>
	/// <param name="pin">whether to pin (true) or unpin (false)</param>
	/// <param name="disableNotification">Pass <c><see langword="true"/></c>, if it is not necessary to send a notification to all chat members about the new pinned message. Notifications are always disabled in channels and private chats</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be unpinned</param>
	public async Task PinUnpinChatMessage(ChatId chatId, int messageId, bool pin = true,
		bool disableNotification = default, string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		if (!pin && messageId == 0)
			if (peer is InputPeerUser user)
				messageId = (await Client.Users_GetFullUser(user)).full_user.pinned_msg_id;
			else
				messageId = (await Client.GetFullChat(peer)).full_chat.PinnedMsg;
		if (businessConnectionId is null)
			await Client.Messages_UpdatePinnedMessage(peer, messageId, silent: disableNotification, unpin: !pin);
		else
			await Client.InvokeWithBusinessConnection(businessConnectionId,
				new Messages_UpdatePinnedMessage { peer = peer, id = messageId,
					flags = (disableNotification ? Messages_UpdatePinnedMessage.Flags.silent : 0)
						| (pin ? 0 : Messages_UpdatePinnedMessage.Flags.unpin) });
	}

	/// <summary>Use this method to clear the list of pinned messages in a chat. If the chat is not a private chat, the bot must be an administrator in the chat for this to work and must have the '<see cref="ChatMemberAdministrator.CanPinMessages"/>' admin right in a supergroup or '<see cref="ChatMemberAdministrator.CanEditMessages"/>' administrator right in a channel</summary>
	/// <remarks>Use messageThreadId=1 for the 'General' topic</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageThreadId">(optional) if you want to target only a specific forum topic</param>
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

	/// <summary>Use this method to get up-to-date information about the chat.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <returns>A <see cref="ChatFullInfo"/> object on success.</returns>
	public async Task<ChatFullInfo> GetChat(ChatId chatId)
	{
		if (chatId.Identifier is long userId && userId >= 0)
		{
			await InitComplete();
			var inputUser = InputUser(userId);
			var userFull = await Client.Users_GetFullUser(inputUser);
			userFull.UserOrChat(_collector);
			var full = userFull.full_user;
			var user = userFull.users[userId];
			var chat = new WTelegram.Types.ChatFullInfo
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
				AcceptedGiftTypes = full.disallowed_gifts.flags.AcceptedGiftTypes(),
				ActiveUsernames = user.username == null && user.usernames == null ? null : [.. user.ActiveUsernames],
				Birthdate = full.birthday.Birthdate(),
				BusinessIntro = await MakeBusinessIntro(full.business_intro),
				BusinessLocation = full.business_location.BusinessLocation(),
				BusinessOpeningHours = full.business_work_hours.BusinessOpeningHours(),
				PersonalChat = full.personal_channel_id == 0 ? null : Chat(full.personal_channel_id),
				EmojiStatusCustomEmojiId = user.emoji_status?.DocumentId.ToString(),
				EmojiStatusExpirationDate = user.emoji_status?.Until,
				Bio = full.about,
				HasPrivateForwards = full.private_forward_name != null,
				HasRestrictedVoiceAndVideoMessages = user.flags.HasFlag(TL.User.Flags.premium) && full.flags.HasFlag(UserFull.Flags.voice_messages_forbidden),
				MessageAutoDeleteTime = full.ttl_period.NullIfZero(),
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
			var chat = new WTelegram.Types.ChatFullInfo
			{
				TLInfo = mcf,
				Id = -tlChat.ID,
				Type = ChatType.Group,
				Title = tlChat.Title,
				Photo = full.ChatPhoto.ChatPhoto(),
				AvailableReactions = full.AvailableReactions switch
				{
					/*chatReactionsNone*/
					null => [],
					ChatReactionsSome crs => [.. crs.reactions.Select(TypesTLConverters.ReactionType)],
					/*chatReactionsAll*/
					_ => null,
				},
				MaxReactionCount = full.AvailableReactions == null ? 0 : Reactions_uniq_max,
				Description = full.About,
				InviteLink = (full.ExportedInvite as ChatInviteExported)?.link,
				MessageAutoDeleteTime = full.TtlPeriod.NullIfZero(),
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
				chat.ActiveUsernames = channel.username == null && channel.usernames == null ? null : [.. channel.ActiveUsernames];
				if (channel.color?.flags.HasFlag(PeerColor.Flags.has_color) == true) chat.AccentColorId = channel.color.color;
				if (channel.color?.flags.HasFlag(PeerColor.Flags.has_background_emoji_id) == true) chat.BackgroundCustomEmojiId = channel.color.background_emoji_id.ToString();
				if (channel.profile_color?.flags.HasFlag(PeerColor.Flags.has_color) == true) chat.ProfileAccentColorId = channel.profile_color.color;
				if (channel.profile_color?.flags.HasFlag(PeerColor.Flags.has_background_emoji_id) == true) chat.ProfileBackgroundCustomEmojiId = channel.profile_color.background_emoji_id.ToString();
				chat.EmojiStatusCustomEmojiId = channel.emoji_status?.DocumentId.ToString();
				chat.EmojiStatusExpirationDate = channel.emoji_status?.Until;
				chat.JoinToSendMessages = channel.flags.HasFlag(Channel.Flags.join_to_send) || !channel.flags.HasFlag(Channel.Flags.megagroup) || channelFull.linked_chat_id == 0;
				chat.JoinByRequest = channel.flags.HasFlag(Channel.Flags.join_request);
				chat.Permissions = (channel.banned_rights ?? channel.default_banned_rights).ChatPermissions();
				chat.CanSendPaidMedia = channelFull.flags2.HasFlag(ChannelFull.Flags2.paid_media_allowed);
				chat.SlowModeDelay = channelFull.slowmode_seconds.NullIfZero();
				chat.UnrestrictBoostCount = channelFull.boosts_unrestrict.NullIfZero();
				chat.HasAggressiveAntiSpamEnabled = channelFull.flags2.HasFlag(ChannelFull.Flags2.antispam);
				chat.HasHiddenMembers = channelFull.flags2.HasFlag(ChannelFull.Flags2.participants_hidden);
				chat.HasVisibleHistory = !channelFull.flags.HasFlag(ChannelFull.Flags.hidden_prehistory);
				chat.HasProtectedContent = channel.flags.HasFlag(Channel.Flags.noforwards);
				chat.StickerSetName = channelFull.stickerset?.short_name;
				var can_send_gift = channelFull.flags2.HasFlag(ChannelFull.Flags2.stargifts_available);
				chat.AcceptedGiftTypes = new() { UnlimitedGifts = can_send_gift, LimitedGifts = can_send_gift, UniqueGifts = can_send_gift };
				chat.CanSetStickerSet = channelFull.flags.HasFlag(ChannelFull.Flags.can_set_stickers);
				chat.CustomEmojiStickerSetName = channelFull.emojiset?.short_name;
				chat.LinkedChatId = channelFull.linked_chat_id == 0 ? null : ZERO_CHANNEL_ID - channelFull.linked_chat_id;
				chat.Location = channelFull.location.ChatLocation();
			}
			else if (tlChat is TL.Chat basicChat)
			{
				chat.Permissions = basicChat.default_banned_rights.ChatPermissions();
				chat.HasProtectedContent = basicChat.flags.HasFlag(TL.Chat.Flags.noforwards);
				chat.AcceptedGiftTypes = new();
				var chatFull = (ChatFull)full;
				if (chatFull.flags.HasFlag(ChatFull.Flags.has_reactions_limit)) chat.MaxReactionCount = chatFull.reactions_limit;
			}
			return chat;
		}
	}

	/// <summary>Use this method to get a list of administrators in a chat, which aren't bots.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <returns>An Array of <see cref="ChatMember"/> objects.</returns>
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
	/// <returns>Returns count on success.</returns>
	public async Task<int> GetChatMemberCount(ChatId chatId)
	{
		var inputPeer = await InputPeerChat(chatId);
		if (inputPeer is InputPeerUser) return 2;
		var chatFull = await Client.GetFullChat(inputPeer);
		chatFull.UserOrChat(_collector);
		return chatFull.full_chat.ParticipantsCount;
	}


	/// <summary>Use this method to get information about a member of a chat. The method is only guaranteed to work for other users if the bot is an administrator in the chat.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup or channel (in the format <c>@channelusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <returns>A <see cref="ChatMember"/> object on success.</returns>
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


	/// <summary>Use this method to delete or set a new group sticker set for a supergroup. The bot must be an administrator in the chat for this to work and must have the appropriate administrator rights. Use the field <em>CanSetStickerSet</em> optionally returned in <see cref="WTelegram.Bot.GetChat">GetChat</see> requests to check if the bot can use this method.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="stickerSetName">Name of the sticker set to be set as the group sticker set (null to delete)</param>
	public async Task SetChatStickerSet(ChatId chatId, string? stickerSetName)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_SetStickers(channel, stickerSetName);
	}

	/// <summary>Use this method to get custom emoji stickers, which can be used as a forum topic icon by any user.</summary>
	/// <returns>An Array of <see cref="Sticker"/> objects.</returns>
	public async Task<Sticker[]> GetForumTopicIconStickers()
	{
		await InitComplete();
		var mss = await Client.Messages_GetStickerSet(new InputStickerSetEmojiDefaultTopicIcons());
		CacheStickerSet(mss);
		var stickers = await mss.documents.OfType<TL.Document>().Select(MakeSticker).WhenAllSequential();
		return stickers;
	}

	/// <summary>Use this method to create a topic in a forum supergroup chat. The bot must be an administrator in the chat for this to work and must have the <em>CanManageTopics</em> administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="name">Topic name, 1-128 characters</param>
	/// <param name="iconColor">Color of the topic icon in RGB format. Currently, must be one of 7322096 (0x6FB9F0), 16766590 (0xFFD67E), 13338331 (0xCB86DB), 9367192 (0x8EEE98), 16749490 (0xFF93B2), or 16478047 (0xFB6F5F)</param>
	/// <param name="iconCustomEmojiId">Unique identifier of the custom emoji shown as the topic icon. Use <see cref="WTelegram.Bot.GetForumTopicIconStickers">GetForumTopicIconStickers</see> to get all allowed custom emoji identifiers.</param>
	/// <returns>Information about the created topic as a <see cref="ForumTopic"/> object.</returns>
	public async Task<ForumTopic> CreateForumTopic(ChatId chatId, string name, int? iconColor = default, string? iconCustomEmojiId = default)
	{
		var channel = await InputChannel(chatId);
		var msg = await PostedMsg(Client.Channels_CreateForumTopic(channel, name, Helpers.RandomLong(), iconColor,
			icon_emoji_id: iconCustomEmojiId == null ? null : long.Parse(iconCustomEmojiId)), channel);
		var ftc = msg.ForumTopicCreated ?? throw new WTException("Channels_CreateForumTopic didn't result in ForumTopicCreated service message");
		return new ForumTopic { MessageThreadId = msg.MessageId, Name = ftc.Name, IconColor = ftc.IconColor, IconCustomEmojiId = ftc.IconCustomEmojiId };
	}

	/// <summary>Use this method to edit name and icon of a topic in a forum supergroup chat. The bot must be an administrator in the chat for this to work and must have the <em>CanManageTopics</em> administrator rights, unless it is the creator of the topic.</summary>
	/// <remarks>Use messageThreadId=1 for the 'General' topic</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread of the forum topic</param>
	/// <param name="name">New topic name, 0-128 characters. If not specified or empty, the current name of the topic will be kept</param>
	/// <param name="iconCustomEmojiId">New unique identifier of the custom emoji shown as the topic icon. Use <see cref="WTelegram.Bot.GetForumTopicIconStickers">GetForumTopicIconStickers</see> to get all allowed custom emoji identifiers. Pass an empty string to remove the icon. If not specified, the current icon will be kept</param>
	public async Task EditForumTopic(ChatId chatId, int messageThreadId, string? name = default, string? iconCustomEmojiId = default)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_EditForumTopic(channel, messageThreadId, name, iconCustomEmojiId == null ? null :
			iconCustomEmojiId == "" ? 0 : long.Parse(iconCustomEmojiId));
	}

	/// <summary>Use this method to close/reopen a topic in a forum supergroup chat. The bot must be an administrator in the chat for this to work and must have the <see cref="ChatAdministratorRights.CanManageTopics"/> administrator rights, unless it is the creator of the topic.</summary>
	/// <remarks>Use messageThreadId=1 for the 'General' topic</remarks>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread of the forum topic</param>
	/// <param name="closed">whether to close (true) or reopen (false) the topic</param>
	public async Task CloseReopenForumTopic(ChatId chatId, int messageThreadId, bool closed = true)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_EditForumTopic(channel, messageThreadId, closed: closed);
	}

	/// <summary>Use this method to delete a forum topic along with all its messages in a forum supergroup chat. The bot must be an administrator in the chat for this to work and must have the <em>CanDeleteMessages</em> administrator rights.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread of the forum topic</param>
	public async Task DeleteForumTopic(ChatId chatId, int messageThreadId)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_DeleteTopicHistory(channel, messageThreadId);
	}

	/// <summary>Use this method to hide or unhide the 'General' topic in a forum supergroup chat. The bot must be an administrator in the chat for this to work and must have the <em>CanManageTopics</em> administrator rights. The topic will be automatically closed if it was open.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target supergroup (in the format <c>@supergroupusername</c>)</param>
	/// <param name="hidden">true to hide, false to unhide</param>
	public async Task HideGeneralForumTopic(ChatId chatId, bool hidden = true)
	{
		var channel = await InputChannel(chatId);
		await Client.Channels_EditForumTopic(channel, 1, hidden: hidden);
	}

	/// <summary>Use this method to send answers to callback queries sent from <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboards</a>. The answer will be displayed to the user as a notification at the top of the chat screen or as an alert</summary>
	/// <remarks>Alternatively, the user can be redirected to the specified Game URL. For this option to work, you must first create a game for your bot via <a href="https://t.me/botfather">@BotFather</a> and accept the terms. Otherwise, you may use links like <c>t.me/your_bot?start=XXXX</c> that open your bot with a parameter.</remarks>
	/// <param name="callbackQueryId">Unique identifier for the query to be answered</param>
	/// <param name="text">Text of the notification. If not specified, nothing will be shown to the user, 0-200 characters</param>
	/// <param name="showAlert">If <see langword="true"/>, an alert will be shown by the client instead of a notification at the top of the chat screen. Defaults to <see langword="false"/>.</param>
	/// <param name="url">URL that will be opened by the user's client. If you have created a <see cref="Game"/> and accepted the conditions via <a href="https://t.me/botfather">@BotFather</a>, specify the URL that opens your game - note that this will only work if the query comes from a <see cref="InlineKeyboardButton"><em>CallbackGame</em></see> button.<br/><br/>Otherwise, you may use links like <c>t.me/your_bot?start=XXXX</c> that open your bot with a parameter.</param>
	/// <param name="cacheTime">The maximum amount of time in seconds that the result of the callback query may be cached client-side. Telegram apps will support caching starting in version 3.14. Defaults to 0.</param>
	public async Task AnswerCallbackQuery(string callbackQueryId, string? text = default, bool showAlert = default,
		string? url = default, int cacheTime = 0)
	{
		await InitComplete();
		await Client.Messages_SetBotCallbackAnswer(long.Parse(callbackQueryId), cacheTime, text, url, showAlert);
	}

	/// <summary>Use this method to get the list of boosts added to a chat by a user. Requires administrator rights in the chat.</summary>
	/// <param name="chatId">Unique identifier for the chat or username of the channel (in the format <c>@channelusername</c>)</param>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <returns>A <see cref="UserChatBoosts"/> object.</returns>
	public async Task<UserChatBoosts> GetUserChatBoosts(ChatId chatId, long userId)
	{
		var peer = await InputPeerChat(chatId);
		var boosts = await Client.Premium_GetUserBoosts(peer, InputUser(userId));
		return new UserChatBoosts { Boosts = await boosts.boosts.Select(MakeBoost).WhenAllSequential() };
	}

	/// <summary>Use this method to get information about the connection of the bot with a business account.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <returns>A <see cref="BusinessConnection"/> object on success.</returns>
	public async Task<BusinessConnection> GetBusinessConnection(string businessConnectionId)
	{
		await InitComplete();
		var updates = await Client.Account_GetBotBusinessConnection(businessConnectionId);
		updates.UserOrChat(_collector);
		var conn = updates.UpdateList.OfType<UpdateBotBusinessConnect>().First().connection;
		return await MakeBusinessConnection(conn);
	}

	/// <summary>Use this method to change the list of the bot's commands. See <a href="https://core.telegram.org/bots/features#commands">this manual</a> for more details about bot commands.</summary>
	/// <param name="commands">A list of bot commands to be set as the list of the bot's commands. At most 100 commands can be specified.</param>
	/// <param name="scope">An object, describing scope of users for which the commands are relevant. Defaults to <see cref="BotCommandScopeDefault"/>.</param>
	/// <param name="languageCode">A two-letter ISO 639-1 language code. If empty, commands will be applied to all users from the given scope, for whose language there are no dedicated commands</param>
	public async Task SetMyCommands(IEnumerable<BotCommand> commands, BotCommandScope? scope = default, string? languageCode = default)
	{
		await Client.Bots_SetBotCommands(await BotCommandScope(scope), languageCode, [.. commands.Select(TypesTLConverters.BotCommand)]);
	}

	/// <summary>Use this method to delete the list of the bot's commands for the given scope and user language. After deletion, <a href="https://core.telegram.org/bots/api#determining-list-of-commands">higher level commands</a> will be shown to affected users.</summary>
	/// <param name="scope">An object, describing scope of users for which the commands are relevant. Defaults to <see cref="BotCommandScopeDefault"/>.</param>
	/// <param name="languageCode">A two-letter ISO 639-1 language code. If empty, commands will be applied to all users from the given scope, for whose language there are no dedicated commands</param>
	public async Task DeleteMyCommands(BotCommandScope? scope = default, string? languageCode = default)
	{
		await Client.Bots_ResetBotCommands(await BotCommandScope(scope), languageCode);
	}

	/// <summary>Use this method to get the current list of the bot's commands for the given scope and user language.</summary>
	/// <param name="scope">An object, describing scope of users. Defaults to <see cref="BotCommandScopeDefault"/>.</param>
	/// <param name="languageCode">A two-letter ISO 639-1 language code or an empty string</param>
	/// <returns>An Array of <see cref="BotCommand"/> objects. If commands aren't set, an empty list is returned.</returns>
	public async Task<BotCommand[]> GetMyCommands(BotCommandScope? scope = default, string? languageCode = default)
	{
		var commands = await Client.Bots_GetBotCommands(await BotCommandScope(scope), languageCode);
		return [.. commands.Select(TypesTLConverters.BotCommand)];
	}

	/// <summary>Use this method to change the bot's name, short description (bio) or description (shown in empty chat).</summary>
	/// <param name="name">New bot name; 0-64 characters. Unchanged if null. Pass an empty string to remove the dedicated name for the given language.</param>
	/// <param name="shortDescription">New short description for the bot; 0-120 characters. Unchanged if null. Pass an empty string to remove the dedicated short description for the given language.</param>
	/// <param name="description">New bot description; 0-512 characters. Unchanged if null. Pass an empty string to remove the dedicated description for the given language.</param>
	/// <param name="languageCode">A two-letter ISO 639-1 language code. If empty, the name will be shown to all users for whose language there is no dedicated name.</param>
	public async Task SetMyInfo(string? name = default, string? shortDescription = default, string? description = default,
		string? languageCode = default)
	{
		await InitComplete();
		await Client.Bots_SetBotInfo(languageCode, name: name, about: shortDescription, description: description);
	}

	/// <summary>Use this method to get the current bot infos for the given user language.</summary>
	/// <param name="languageCode">A two-letter ISO 639-1 language code or an empty string</param>
	/// <returns>Returns bot name, short description (bio) and description (shown in empty chat) on success.</returns>
	public async Task<(string name, string shortDescription, string description)> GetMyInfo(string? languageCode = default)
	{
		await InitComplete();
		var botInfo = await Client.Bots_GetBotInfo(languageCode);
		return (botInfo.name, botInfo.about, botInfo.description);
	}

	/// <summary>Use this method to change the bot's menu button in a private chat, or the default menu button.</summary>
	/// <param name="chatId">Unique identifier for the target private chat. If not specified, default bot's menu button will be changed</param>
	/// <param name="menuButton">An object for the bot's new menu button. Defaults to <see cref="MenuButtonDefault"/></param>
	public async Task SetChatMenuButton(long? chatId = default, MenuButton? menuButton = default)
	{
		await InitComplete();
		var user = chatId.HasValue ? InputUser(chatId.Value) : null;
		await Client.Bots_SetBotMenuButton(user, menuButton.BotMenuButton());
	}

	/// <summary>Use this method to get the current value of the bot's menu button in a private chat, or the default menu button.</summary>
	/// <param name="chatId">Unique identifier for the target private chat. If not specified, default bot's menu button will be returned</param>
	/// <returns><see cref="MenuButton"/> on success.</returns>
	public async Task<MenuButton> GetChatMenuButton(long? chatId = default)
	{
		await InitComplete();
		var user = chatId.HasValue ? InputUser(chatId.Value) : null;
		var botMenuButton = await Client.Bots_GetBotMenuButton(user);
		return botMenuButton.MenuButton();
	}

	/// <summary>Use this method to change the default administrator rights requested by the bot when it's added as an administrator to groups or channels. These rights will be suggested to users, but they are free to modify the list before adding the bot.</summary>
	/// <param name="rights">An object describing new default administrator rights. If not specified, the default administrator rights will be cleared.</param>
	/// <param name="forChannels">Pass <see langword="true"/> to change the default administrator rights of the bot in channels. Otherwise, the default administrator rights of the bot for groups and supergroups will be changed.</param>
	public async Task SetMyDefaultAdministratorRights(ChatAdministratorRights? rights = default, bool forChannels = default)
	{
		await InitComplete();
		var admin_rights = rights.ChatAdminRights();
		if (forChannels)
			await Client.Bots_SetBotBroadcastDefaultAdminRights(admin_rights);
		else
			await Client.Bots_SetBotGroupDefaultAdminRights(admin_rights);
	}

	/// <summary>Use this method to get the current default administrator rights of the bot.</summary>
	/// <param name="forChannels">Pass <see langword="true"/> to get default administrator rights of the bot in channels. Otherwise, default administrator rights of the bot for groups and supergroups will be returned.</param>
	/// <returns><see cref="ChatAdministratorRights"/> on success.</returns>
	public async Task<ChatAdministratorRights> GetMyDefaultAdministratorRights(bool forChannels = default)
	{
		await InitComplete();
		var full = await Client.Users_GetFullUser(Client.User);
		return (forChannels ? full.full_user.bot_broadcast_admin_rights : full.full_user.bot_group_admin_rights).ChatAdministratorRights();
	}
	#endregion Available methods

	#region Updating messages

	/// <summary>Use this method to edit text and <a href="https://core.telegram.org/bots/api#games">game</a> messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="text">New text of the message, 1-4096 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the message text. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="entities">A list of special entities that appear in message text, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="linkPreviewOptions">Link preview generation options for the message</param>
	/// <param name="replyMarkup">An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	/// <returns>The edited <see cref="Message"/> is returned</returns>
	public async Task<Message> EditMessageText(ChatId chatId, int messageId, string text, ParseMode parseMode = default,
		IEnumerable<MessageEntity>? entities = default, LinkPreviewOptions? linkPreviewOptions = default, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var tlEntities = ApplyParse(parseMode, ref text!, entities);
		var peer = await InputPeerChat(chatId);
		var media = linkPreviewOptions.InputMediaWebPage();
		return await PostedMsg(Messages_EditMessage(businessConnectionId, peer, messageId, text, media,
			await MakeReplyMarkup(replyMarkup), tlEntities, no_webpage: linkPreviewOptions?.IsDisabled == true, invert_media: linkPreviewOptions?.ShowAboveText == true), peer, text, bConnId: businessConnectionId);
	}

	/// <summary>Use this method to edit text and <a href="https://core.telegram.org/bots/api#games">game</a> messages.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="text">New text of the message, 1-4096 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the message text. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="entities">A list of special entities that appear in message text, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="linkPreviewOptions">Link preview generation options for the message</param>
	/// <param name="replyMarkup">An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	public async Task EditMessageText(string inlineMessageId, string text, ParseMode parseMode = default, IEnumerable<MessageEntity>? entities = default,
		LinkPreviewOptions? linkPreviewOptions = default, InlineKeyboardMarkup? replyMarkup = default, string? businessConnectionId = default)
	{
		var tlEntities = ApplyParse(parseMode, ref text!, entities);
		var id = await ParseInlineMsgID(inlineMessageId);
		var media = linkPreviewOptions.InputMediaWebPage();
		await Messages_EditInlineBotMessage(businessConnectionId, id, text, media,
			await MakeReplyMarkup(replyMarkup), tlEntities, linkPreviewOptions?.IsDisabled == true, linkPreviewOptions?.ShowAboveText == true);
	}

	/// <summary>Use this method to edit captions of messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="caption">New caption of the message, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the message caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media. Supported only for animation, photo and video messages.</param>
	/// <param name="replyMarkup">An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	/// <returns>The edited <see cref="Message"/> is returned</returns>
	public async Task<Message> EditMessageCaption(ChatId chatId, int messageId, string? caption, ParseMode parseMode = default,
		IEnumerable<MessageEntity>? captionEntities = default, bool showCaptionAboveMedia = default, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var entities = ApplyParse(parseMode, ref caption!, captionEntities);
		var peer = await InputPeerChat(chatId);
		return await PostedMsg(Messages_EditMessage(businessConnectionId, peer, messageId, caption, null,
			await MakeReplyMarkup(replyMarkup), entities, invert_media: showCaptionAboveMedia), peer, caption, bConnId: businessConnectionId);
	}

	/// <summary>Use this method to edit captions of messages.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="caption">New caption of the message, 0-1024 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the message caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="showCaptionAboveMedia">Pass <see langword="true"/>, if the caption must be shown above the message media. Supported only for animation, photo and video messages.</param>
	/// <param name="replyMarkup">An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	public async Task EditMessageCaption(string inlineMessageId, string? caption, ParseMode parseMode = default, IEnumerable<MessageEntity>? captionEntities = default,
		bool showCaptionAboveMedia = default, InlineKeyboardMarkup? replyMarkup = default, string? businessConnectionId = default)
	{
		var entities = ApplyParse(parseMode, ref caption!, captionEntities);
		var id = await ParseInlineMsgID(inlineMessageId);
		await Messages_EditInlineBotMessage(businessConnectionId, id, caption, null, await MakeReplyMarkup(replyMarkup), entities, invert_media: showCaptionAboveMedia);
	}

	/// <summary>Use this method to edit animation, audio, document, photo, or video messages, or to add media to text messages. If a message is part of a message album, then it can be edited only to an audio for audio albums, only to a document for document albums and to a photo or a video otherwise. When an inline message is edited, a new file can't be uploaded; use a previously uploaded file via its FileId or specify a URL.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="media">An object for a new media content of the message</param>
	/// <param name="replyMarkup">An object for a new <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	/// <returns>The edited <see cref="Message"/> is returned</returns>
	public async Task<Message> EditMessageMedia(ChatId chatId, int messageId, InputMedia media, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var ism = await InputSingleMedia(peer, media);
		return await PostedMsg(Messages_EditMessage(businessConnectionId, peer, messageId, ism.message ?? "", ism.media,
			await MakeReplyMarkup(replyMarkup), ism.entities), peer, bConnId: businessConnectionId);
	}

	/// <summary>Use this method to edit animation, audio, document, photo, or video messages, or to add media to text messages. If a message is part of a message album, then it can be edited only to an audio for audio albums, only to a document for document albums and to a photo or a video otherwise. When an inline message is edited, a new file can't be uploaded; use a previously uploaded file via its FileId or specify a URL.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="media">An object for a new media content of the message</param>
	/// <param name="replyMarkup">An object for a new <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	public async Task EditMessageMedia(string inlineMessageId, InputMedia media, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var id = await ParseInlineMsgID(inlineMessageId);
		var ism = await InputSingleMedia(InputPeer.Self, media);
		await Messages_EditInlineBotMessage(businessConnectionId, id, ism.message ?? "", ism.media,
			await MakeReplyMarkup(replyMarkup), ism.entities);
	}

	/// <summary>Use this method to edit live location messages. A location can be edited until its <paramref name="livePeriod"/> expires or editing is explicitly disabled by a call to <see cref="WTelegram.Bot.StopMessageLiveLocation">StopMessageLiveLocation</see>.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="latitude">Latitude of new location</param>
	/// <param name="longitude">Longitude of new location</param>
	/// <param name="livePeriod">New period in seconds during which the location can be updated, starting from the message send date. If 0x7FFFFFFF is specified, then the location can be updated forever. Otherwise, the new value must not exceed the current <paramref name="livePeriod"/> by more than a day, and the live location expiration date must remain within the next 90 days. If not specified, then <paramref name="livePeriod"/> remains unchanged</param>
	/// <param name="horizontalAccuracy">The radius of uncertainty for the location, measured in meters; 0-1500</param>
	/// <param name="heading">Direction in which the user is moving, in degrees. Must be between 1 and 360 if specified.</param>
	/// <param name="proximityAlertRadius">The maximum distance for proximity alerts about approaching another chat member, in meters. Must be between 1 and 100000 if specified.</param>
	/// <param name="replyMarkup">An object for a new <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	/// <returns>The edited <see cref="Message"/> is returned</returns>
	public async Task<Message> EditMessageLiveLocation(ChatId chatId, int messageId, double latitude, double longitude,
		int livePeriod = 0, int horizontalAccuracy = 0, int heading = 0, int proximityAlertRadius = 0, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var media = MakeGeoLive(latitude, longitude, horizontalAccuracy, heading, proximityAlertRadius, livePeriod);
		return await PostedMsg(Messages_EditMessage(businessConnectionId, peer, messageId, null, media, await MakeReplyMarkup(replyMarkup)), peer, bConnId: businessConnectionId);
	}

	/// <summary>Use this method to edit live location messages. A location can be edited until its <paramref name="livePeriod"/> expires or editing is explicitly disabled by a call to <see cref="WTelegram.Bot.StopMessageLiveLocation">StopMessageLiveLocation</see>.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="latitude">Latitude of new location</param>
	/// <param name="longitude">Longitude of new location</param>
	/// <param name="livePeriod">New period in seconds during which the location can be updated, starting from the message send date. If 0x7FFFFFFF is specified, then the location can be updated forever. Otherwise, the new value must not exceed the current <paramref name="livePeriod"/> by more than a day, and the live location expiration date must remain within the next 90 days. If not specified, then <paramref name="livePeriod"/> remains unchanged</param>
	/// <param name="horizontalAccuracy">The radius of uncertainty for the location, measured in meters; 0-1500</param>
	/// <param name="heading">Direction in which the user is moving, in degrees. Must be between 1 and 360 if specified.</param>
	/// <param name="proximityAlertRadius">The maximum distance for proximity alerts about approaching another chat member, in meters. Must be between 1 and 100000 if specified.</param>
	/// <param name="replyMarkup">An object for a new <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	public async Task EditMessageLiveLocation(string inlineMessageId, double latitude, double longitude, int livePeriod = 0,
		int horizontalAccuracy = 0, int heading = 0, int proximityAlertRadius = 0, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var id = await ParseInlineMsgID(inlineMessageId);
		var media = MakeGeoLive(latitude, longitude, horizontalAccuracy, heading, proximityAlertRadius, livePeriod);
		await Messages_EditInlineBotMessage(businessConnectionId, id, null, media, await MakeReplyMarkup(replyMarkup));
	}

	/// <summary>Use this method to stop updating a live location message before <em>LivePeriod</em> expires.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message with live location to stop</param>
	/// <param name="replyMarkup">An object for a new <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	/// <returns>The edited <see cref="Message"/> is returned</returns>
	public async Task<Message> StopMessageLiveLocation(ChatId chatId, int messageId, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var media = new InputMediaGeoLive { flags = InputMediaGeoLive.Flags.stopped };
		return await PostedMsg(Messages_EditMessage(businessConnectionId, peer, messageId, null, media, await MakeReplyMarkup(replyMarkup)), peer, bConnId: businessConnectionId);
	}

	/// <summary>Use this method to stop updating a live location message before <em>LivePeriod</em> expires.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="replyMarkup">An object for a new <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	public async Task StopMessageLiveLocation(string inlineMessageId, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var id = await ParseInlineMsgID(inlineMessageId);
		var media = new InputMediaGeoLive { flags = InputMediaGeoLive.Flags.stopped };
		await Messages_EditInlineBotMessage(businessConnectionId, id, null, media, await MakeReplyMarkup(replyMarkup));
	}

	/// <summary>Use this method to edit only the reply markup of messages.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the message to edit</param>
	/// <param name="replyMarkup">An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	/// <returns>The edited <see cref="Message"/> is returned</returns>
	public async Task<Message> EditMessageReplyMarkup(ChatId chatId, int messageId, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		return await PostedMsg(Messages_EditMessage(businessConnectionId, peer, messageId, null, null, await MakeReplyMarkup(replyMarkup)), peer, bConnId: businessConnectionId);
	}

	/// <summary>Use this method to edit only the reply markup of messages.</summary>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="replyMarkup">An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	public async Task EditMessageReplyMarkup(string inlineMessageId, InlineKeyboardMarkup? replyMarkup = default, string? businessConnectionId = default)
	{
		var id = await ParseInlineMsgID(inlineMessageId);
		await Messages_EditInlineBotMessage(businessConnectionId, id, reply_markup: await MakeReplyMarkup(replyMarkup));
	}

	/// <summary>Use this method to stop a poll which was sent by the bot.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageId">Identifier of the original message with the poll</param>
	/// <param name="replyMarkup">An object for a new message <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message to be edited was sent</param>
	/// <returns>The stopped <see cref="Poll"/> is returned.</returns>
	public async Task<Telegram.Bot.Types.Poll> StopPoll(ChatId chatId, int messageId, InlineKeyboardMarkup? replyMarkup = default,
		string? businessConnectionId = default)
	{
		var peer = await InputPeerChat(chatId);
		var closedPoll = new InputMediaPoll { poll = new() { flags = TL.Poll.Flags.closed } };
		var updates = await Messages_EditMessage(businessConnectionId, peer, messageId, null, closedPoll, await MakeReplyMarkup(replyMarkup));
		updates.UserOrChat(_collector);
		var ump = updates.UpdateList.OfType<UpdateMessagePoll>().First();
		return MakePoll(ump.poll, ump.results);
	}

	/// <summary>Use this method to delete multiple messages simultaneously. If some of the specified messages can't be found, they are skipped.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="messageIds">A list of 1-100 identifiers of messages to delete. See <see cref="WTelegram.Bot.DeleteMessage">DeleteMessage</see> for limitations on which messages can be deleted</param>
	public async Task DeleteMessages(ChatId chatId, params int[] messageIds)
	{
		await Client.DeleteMessages(await InputPeerChat(chatId), messageIds);
	}

	/// <summary>Returns the list of gifts that can be sent by the bot to users and channel chats.</summary>
	/// <returns>A <see cref="GiftList"/> object.</returns>
	public async Task<GiftList> GetAvailableGifts()
	{
		await InitComplete();
		var starGifts = await Client.Payments_GetStarGifts();
		var gifts = starGifts.gifts.OfType<StarGift>().Where(g => !g.flags.HasFlag(StarGift.Flags.sold_out)).Select(MakeGift).ToArray();
		return new GiftList { Gifts = gifts };
	}

	/// <summary>Sends a gift to the given user or channel chat. The gift can't be converted to Telegram Stars by the receiver.</summary>
	/// <param name="chatId">Unique identifier of the target user, chat or username of the channel (in the format <c>@channelusername</c>) that will receive the gift.</param>
	/// <param name="giftId">Identifier of the gift</param>
	/// <param name="text">Text that will be shown along with the gift; 0-128 characters</param>
	/// <param name="textParseMode">Mode for parsing entities in the text. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details. Entities other than <see cref="MessageEntityType.Bold">Bold</see>, <see cref="MessageEntityType.Italic">Italic</see>, <see cref="MessageEntityType.Underline">Underline</see>, <see cref="MessageEntityType.Strikethrough">Strikethrough</see>, <see cref="MessageEntityType.Spoiler">Spoiler</see>, and <see cref="MessageEntityType.CustomEmoji">CustomEmoji</see> are ignored.</param>
	/// <param name="textEntities">A list of special entities that appear in the gift text. It can be specified instead of <paramref name="textParseMode"/>. Entities other than <see cref="MessageEntityType.Bold">Bold</see>, <see cref="MessageEntityType.Italic">Italic</see>, <see cref="MessageEntityType.Underline">Underline</see>, <see cref="MessageEntityType.Strikethrough">Strikethrough</see>, <see cref="MessageEntityType.Spoiler">Spoiler</see>, and <see cref="MessageEntityType.CustomEmoji">CustomEmoji</see> are ignored.</param>
	/// <param name="payForUpgrade">Pass <see langword="true"/> to pay for the gift upgrade from the bot's balance, thereby making the upgrade free for the receiver</param>
	public async Task SendGift(ChatId chatId, string giftId, string? text = default, ParseMode textParseMode = default,
		IEnumerable<MessageEntity>? textEntities = default, bool payForUpgrade = default)
	{
		await InitComplete();
		var entities = ApplyParse(textParseMode, ref text!, textEntities);
		var invoice = new InputInvoiceStarGift
		{
			flags = text != null ? InputInvoiceStarGift.Flags.has_message : 0
				| (payForUpgrade ? InputInvoiceStarGift.Flags.include_upgrade : 0),
			peer = await InputPeerChat(chatId),
			gift_id = long.Parse(giftId),
			message = new() { text = text, entities = entities },
		};
		var paymentForm = await Client.Payments_GetPaymentForm(invoice);
		if (paymentForm is not TL.Payments_PaymentFormStarGift starGift) throw new RpcException(500, "Unsupported");
		await Client.Payments_SendStarsForm(starGift.form_id, invoice);
	}

	/// <summary>Gifts a Telegram Premium subscription to the given user.</summary>
	/// <param name="userId">Unique identifier of the target user who will receive a Telegram Premium subscription</param>
	/// <param name="monthCount">Number of months the Telegram Premium subscription will be active for the user; must be one of 3, 6, or 12</param>
	/// <param name="starCount">Number of Telegram Stars to pay for the Telegram Premium subscription; must be 1000 for 3 months, 1500 for 6 months, and 2500 for 12 months</param>
	/// <param name="text">Text that will be shown along with the service message about the subscription; 0-128 characters</param>
	/// <param name="textParseMode">Mode for parsing entities in the text. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details. Entities other than <see cref="MessageEntityType.Bold">Bold</see>, <see cref="MessageEntityType.Italic">Italic</see>, <see cref="MessageEntityType.Underline">Underline</see>, <see cref="MessageEntityType.Strikethrough">Strikethrough</see>, <see cref="MessageEntityType.Spoiler">Spoiler</see>, and <see cref="MessageEntityType.CustomEmoji">CustomEmoji</see> are ignored.</param>
	/// <param name="textEntities">A list of special entities that appear in the gift text. It can be specified instead of <paramref name="textParseMode"/>. Entities other than “bold”, “italic”, “underline”, “strikethrough”, “spoiler”, and “CustomEmoji” are ignored.</param>
	public async Task GiftPremiumSubscription(long userId, int monthCount, int starCount, string? text = default, ParseMode textParseMode = default,
		IEnumerable<MessageEntity>? textEntities = default)
	{
		await InitComplete();
		var entities = ApplyParse(textParseMode, ref text!, textEntities);
		var invoice = new InputInvoicePremiumGiftStars { user_id = InputUser(userId), months = monthCount };
		var ppfb = await Client.Payments_GetPaymentForm(invoice);
		if (ppfb is not Payments_PaymentFormStars) throw new RpcException(500, "Unsupported");
		if (ppfb.Invoice.prices is not [{ amount: var amount }] || amount != starCount) throw new RpcException(400, "Wrong purchase price specified");
		if (text != null)
		{
			invoice.flags |= InputInvoicePremiumGiftStars.Flags.has_message;
			invoice.message = new TextWithEntities() { text = text, entities = entities };
		}
		await Client.Payments_SendStarsForm(ppfb.FormId, invoice);
	}

	/// <summary>Verifies a user <a href="https://telegram.org/verify#third-party-verification">on behalf of the organization</a> which is represented by the bot.</summary>
	/// <param name="userId">Unique identifier of the target user</param>
	/// <param name="customDescription">Custom description for the verification; 0-70 characters. Must be empty if the organization isn't allowed to provide a custom verification description.</param>
	public async Task VerifyUser(long userId, string? customDescription = default)
	{
		await InitComplete();
		await Client.Bots_SetCustomVerification(InputUser(userId), null, customDescription, true);
	}

	/// <summary>Verifies a chat <a href="https://telegram.org/verify#third-party-verification">on behalf of the organization</a> which is represented by the bot.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="customDescription">Custom description for the verification; 0-70 characters. Must be empty if the organization isn't allowed to provide a custom verification description.</param>
	public async Task VerifyChat(ChatId chatId, string? customDescription = default)
	{
		var peer = await InputPeerChat(chatId);
		await Client.Bots_SetCustomVerification(peer, null, customDescription, true);
	}

	/// <summary>Removes verification from a user who is currently verified <a href="https://telegram.org/verify#third-party-verification">on behalf of the organization</a> represented by the bot.</summary>
	/// <param name="userId">Unique identifier of the target user</param>
	public async Task RemoveUserVerification(long userId)
	{
		await InitComplete();
		await Client.Bots_SetCustomVerification(InputUser(userId), enabled: false);
	}

	/// <summary>Removes verification from a chat that is currently verified <a href="https://telegram.org/verify#third-party-verification">on behalf of the organization</a> represented by the bot.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	public async Task RemoveChatVerification(ChatId chatId)
	{
		var peer = await InputPeerChat(chatId);
		await Client.Bots_SetCustomVerification(peer, enabled: false);
	}

	/// <summary>Marks incoming message as read on behalf of a business account. Requires the <em>CanReadMessages</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which to read the message</param>
	/// <param name="chatId">Unique identifier of the chat in which the message was received. The chat must have been active in the last 24 hours.</param>
	/// <param name="messageId">Unique identifier of the message to mark as read</param>
	public async Task ReadBusinessMessage(string businessConnectionId, long chatId, int messageId)
	{
		var peer = await InputPeerChat(chatId);
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Messages_ReadHistory { peer = peer, max_id = messageId });
	}

	/// <summary>Delete messages on behalf of a business account. Requires the <em>CanDeleteSentMessages</em> business bot right to delete messages sent by the bot itself, or the <em>CanDeleteAllMessages</em> business bot right to delete any message.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which to delete the messages</param>
	/// <param name="messageIds">A list of 1-100 identifiers of messages to delete. All messages must be from the same chat. See <see cref="WTelegram.Bot.DeleteMessage">DeleteMessage</see> for limitations on which messages can be deleted</param>
	public async Task DeleteBusinessMessages(string businessConnectionId, IEnumerable<int> messageIds)
	{
		await InitComplete();
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Messages_DeleteMessages { id = [.. messageIds] });
	}

	/// <summary>Changes the first and last name of a managed business account. Requires the <em>CanChangeName</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="firstName">The new value of the first name for the business account; 1-64 characters</param>
	/// <param name="lastName">The new value of the last name for the business account; 0-64 characters</param>
	public async Task SetBusinessAccountName(string businessConnectionId, string firstName, string? lastName = default)
	{
		await InitComplete();
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Account_UpdateProfile {
				flags = Account_UpdateProfile.Flags.has_first_name | Account_UpdateProfile.Flags.has_last_name,
				first_name = firstName, last_name = lastName });
	}

	/// <summary>Changes the username of a managed business account. Requires the <em>CanChangeUsername</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="username">The new value of the username for the business account; 0-32 characters</param>
	public async Task SetBusinessAccountUsername(string businessConnectionId, string? username = default)
	{
		await InitComplete();
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Account_UpdateUsername { username = username });
	}

	/// <summary>Changes the bio of a managed business account. Requires the <em>CanChangeBio</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="bio">The new value of the bio for the business account; 0-140 characters</param>
	public async Task SetBusinessAccountBio(string businessConnectionId, string? bio = default)
	{
		await InitComplete();
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Account_UpdateProfile { flags = Account_UpdateProfile.Flags.has_about, about = bio });
	}

	/// <summary>Changes the profile photo of a managed business account. Requires the <em>CanEditProfilePhoto</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="photo">The new profile photo to set</param>
	/// <param name="isPublic">Pass <see langword="true"/> to set the public photo, which will be visible even if the main photo is hidden by the business account's privacy settings. An account can have only one public photo.</param>
	public async Task SetBusinessAccountProfilePhoto(string businessConnectionId, InputProfilePhoto photo, bool isPublic = default)
	{
		//TODO: check why tdlib doesn't use InvokeWithBusinessConnection
		var peer = await GetBusinessPeer(businessConnectionId);
		switch (photo)
		{
			case InputProfilePhotoStatic ipps:
				if (ipps.Photo.FileType != FileType.Stream) throw new RpcException(400, "Photo must be uploaded as a file");
				var imup = (InputMediaUploadedPhoto)await InputMediaPhoto(ipps.Photo);
				await Client.InvokeWithBusinessConnection(businessConnectionId,
					new Photos_UploadProfilePhoto { file = imup.file,
						flags = Photos_UploadProfilePhoto.Flags.has_file | (isPublic ? Photos_UploadProfilePhoto.Flags.fallback : 0) });
				break;
			case InputProfilePhotoAnimated ippa:
				if (ippa.Animation.FileType != FileType.Stream) throw new RpcException(400, "Photo must be uploaded as a file");
				var imud = (InputMediaUploadedDocument)await InputMediaDocument(ippa.Animation);
				await Client.InvokeWithBusinessConnection(businessConnectionId,
					new Photos_UploadProfilePhoto { video = imud.file, video_start_ts =	ippa.MainFrameTimestamp ?? 0.0,
						flags = Photos_UploadProfilePhoto.Flags.has_video | (isPublic ? Photos_UploadProfilePhoto.Flags.fallback : 0)
							| (ippa.MainFrameTimestamp.HasValue ? Photos_UploadProfilePhoto.Flags.has_video_start_ts : 0)});
				break;
		}
	}

	/// <summary>Removes the current profile photo of a managed business account. Requires the <em>CanEditProfilePhoto</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="isPublic">Pass <see langword="true"/> to remove the public photo, which is visible even if the main photo is hidden by the business account's privacy settings. After the main photo is removed, the previous profile photo (if present) becomes the main photo.</param>
	public async Task RemoveBusinessAccountProfilePhoto(string businessConnectionId, bool isPublic = default)
	{
		await InitComplete();
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Photos_UpdateProfilePhoto { flags = isPublic ? Photos_UpdateProfilePhoto.Flags.fallback : 0});
	}

	/// <summary>Changes the privacy settings pertaining to incoming gifts in a managed business account. Requires the <em>CanChangeGiftSettings</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="showGiftButton">Pass <see langword="true"/>, if a button for sending a gift to the user or by the business account must always be shown in the input field</param>
	/// <param name="acceptedGiftTypes">Types of gifts accepted by the business account</param>
	public async Task SetBusinessAccountGiftSettings(string businessConnectionId, bool showGiftButton, AcceptedGiftTypes acceptedGiftTypes)
	{
		await InitComplete();
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Account_SetGlobalPrivacySettings { settings = new() {
				flags = GlobalPrivacySettings.Flags.has_disallowed_gifts | (showGiftButton ? GlobalPrivacySettings.Flags.display_gifts_button : 0),
				disallowed_gifts = new() {
					flags = (acceptedGiftTypes.UnlimitedGifts ? 0 : DisallowedGiftsSettings.Flags.disallow_unlimited_stargifts) |
							(acceptedGiftTypes.LimitedGifts ? 0 : DisallowedGiftsSettings.Flags.disallow_limited_stargifts) |
							(acceptedGiftTypes.UniqueGifts ? 0 : DisallowedGiftsSettings.Flags.disallow_unique_stargifts) |
							(acceptedGiftTypes.PremiumSubscription ? 0 : DisallowedGiftsSettings.Flags.disallow_premium_gifts) } } });
	}

	/// <summary>Returns the amount of Telegram Stars owned by a managed business account. Requires the <em>CanViewGiftsAndStars</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <returns><see cref="StarAmount"/> on success.</returns>
	public async Task<StarAmount> GetBusinessAccountStarBalance(string businessConnectionId)
	{
		var peer = await GetBusinessPeer(businessConnectionId);
		var pss = await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Payments_GetStarsStatus { peer = peer });
		return new StarAmount { Amount = (int)pss.balance.amount, NanostarAmount = pss.balance.nanos };
	}

	/// <summary>Transfers Telegram Stars from the business account balance to the bot's balance. Requires the <em>CanTransferStars</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="starCount">Number of Telegram Stars to transfer; 1-10000</param>
	public async Task TransferBusinessAccountStars(string businessConnectionId, int starCount)
	{
		var peer = await GetBusinessPeer(businessConnectionId);
		var invoice = new InputInvoiceBusinessBotTransferStars { bot = TL.InputUser.Self, stars = starCount };
		var ppfb = await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Payments_GetPaymentForm { invoice = invoice });
		if (ppfb is Payments_PaymentForm) throw new RpcException(500, "Unsupported");
		if (ppfb.Invoice.prices is not [{ amount: var amount }] || amount != starCount) throw new RpcException(400, "Wrong transfer price specified");
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Payments_SendStarsForm { form_id = ppfb.FormId, invoice = invoice });
	}

	/// <summary>Returns the gifts received and owned by a managed business account. Requires the <em>CanViewGiftsAndStars</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="excludeUnsaved">Pass <see langword="true"/> to exclude gifts that aren't saved to the account's profile page</param>
	/// <param name="excludeSaved">Pass <see langword="true"/> to exclude gifts that are saved to the account's profile page</param>
	/// <param name="excludeUnlimited">Pass <see langword="true"/> to exclude gifts that can be purchased an unlimited number of times</param>
	/// <param name="excludeLimited">Pass <see langword="true"/> to exclude gifts that can be purchased a limited number of times</param>
	/// <param name="excludeUnique">Pass <see langword="true"/> to exclude unique gifts</param>
	/// <param name="sortByPrice">Pass <see langword="true"/> to sort results by gift price instead of send date. Sorting is applied before pagination.</param>
	/// <param name="offset">Offset of the first entry to return as received from the previous request; use empty string to get the first chunk of results</param>
	/// <param name="limit">The maximum number of gifts to be returned; 1-100. Defaults to 100</param>
	/// <returns><see cref="OwnedGifts"/> on success.</returns>
	public async Task<OwnedGifts> GetBusinessAccountGifts(string businessConnectionId, bool excludeUnsaved = default,
		bool excludeSaved = default, bool excludeUnlimited = default, bool excludeLimited = default, bool excludeUnique = default,
		bool sortByPrice = default, string? offset = default, int? limit = default)
	{
		var peer = await GetBusinessPeer(businessConnectionId);
		var pssg = await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Payments_GetSavedStarGifts
			{
				flags = (excludeUnsaved ? Payments_GetSavedStarGifts.Flags.exclude_unsaved : 0)
					| (excludeSaved ? Payments_GetSavedStarGifts.Flags.exclude_saved : 0)
					| (excludeUnlimited ? Payments_GetSavedStarGifts.Flags.exclude_unlimited : 0)
					| (excludeLimited ? Payments_GetSavedStarGifts.Flags.exclude_limited : 0)
					| (excludeUnique ? Payments_GetSavedStarGifts.Flags.exclude_unique : 0)
					| (sortByPrice ? Payments_GetSavedStarGifts.Flags.sort_by_value : 0),
				peer = peer,
				offset = offset,
				limit = limit ?? 100
			});
		pssg.UserOrChat(_collector);
		return new OwnedGifts
		{
			Gifts = await pssg.gifts.Select(OwnedGift).WhenAllSequential(),
			NextOffset = pssg.next_offset,
			TotalCount = pssg.count
		};
	}

	/// <summary>Converts a given regular gift to Telegram Stars. Requires the <em>CanConvertGiftsToStars</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="ownedGiftId">Unique identifier of the regular gift that should be converted to Telegram Stars</param>
	public async Task ConvertGiftToStars(string businessConnectionId, string ownedGiftId)
	{
		var stargift = await InputSavedStarGift(ownedGiftId);
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Payments_ConvertStarGift { stargift = stargift });
	}

	/// <summary>Upgrades a given regular gift to a unique gift. Requires the <em>CanTransferAndUpgradeGifts</em> business bot right. Additionally requires the <em>CanTransferStars</em> business bot right if the upgrade is paid.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="ownedGiftId">Unique identifier of the regular gift that should be upgraded to a unique one</param>
	/// <param name="keepOriginalDetails">Pass <see langword="true"/> to keep the original gift text, sender and receiver in the upgraded gift</param>
	/// <param name="starCount">The amount of Telegram Stars that will be paid for the upgrade from the business account balance. If <c>gift.PrepaidUpgradeStarCount &gt; 0</c>, then pass 0, otherwise, the <em>CanTransferStars</em> business bot right is required and <c>gift.UpgradeStarCount</c> must be passed.</param>
	public async Task UpgradeGift(string businessConnectionId, string ownedGiftId, bool keepOriginalDetails = default,
		int? starCount = default)
	{
		var stargift = await InputSavedStarGift(ownedGiftId);
		if (starCount != 0)
		{
			var invoice = new InputInvoiceStarGiftUpgrade { stargift = stargift,
				flags = keepOriginalDetails ? InputInvoiceStarGiftUpgrade.Flags.keep_original_details : 0 };
			var ppfb = await Client.InvokeWithBusinessConnection(businessConnectionId,
				new Payments_GetPaymentForm { invoice = invoice });
			if (ppfb is not Payments_PaymentFormStarGift) throw new RpcException(500, "Unsupported");
			if (ppfb.Invoice.prices is not [{ amount: var amount }] || amount != starCount) throw new RpcException(400, "Wrong upgrade price specified");
			await Client.InvokeWithBusinessConnection(businessConnectionId,
				new Payments_SendStarsForm { form_id = ppfb.FormId, invoice = invoice });
		}
		else
		{
			await Client.InvokeWithBusinessConnection(businessConnectionId,
				new Payments_UpgradeStarGift { stargift = stargift,
					flags = keepOriginalDetails ? Payments_UpgradeStarGift.Flags.keep_original_details : 0 });
		}
	}

	/// <summary>Transfers an owned unique gift to another user. Requires the <em>CanTransferAndUpgradeGifts</em> business bot right. Requires <em>CanTransferStars</em> business bot right if the transfer is paid.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="ownedGiftId">Unique identifier of the regular gift that should be transferred</param>
	/// <param name="newOwnerChatId">Unique identifier of the chat which will own the gift. The chat must be active in the last 24 hours.</param>
	/// <param name="starCount">The amount of Telegram Stars that will be paid for the transfer from the business account balance. If positive, then the <em>CanTransferStars</em> business bot right is required.</param>
	public async Task TransferGift(string businessConnectionId, string ownedGiftId, long newOwnerChatId, int? starCount = default)
	{
		var peer = await InputPeerChat(newOwnerChatId);
		var stargift = await InputSavedStarGift(ownedGiftId);
		if (starCount != 0)
		{
			var invoice = new InputInvoiceStarGiftTransfer { stargift = stargift, to_id = peer };
			var ppfb = await Client.InvokeWithBusinessConnection(businessConnectionId,
				new Payments_GetPaymentForm { invoice = invoice });
			if (ppfb is not Payments_PaymentFormStarGift) throw new RpcException(500, "Unsupported");
			if (ppfb.Invoice.prices is not [{ amount: var amount }] || amount != starCount) throw new RpcException(400, "Wrong transfer price specified");
			await Client.InvokeWithBusinessConnection(businessConnectionId,
				new Payments_SendStarsForm { form_id = ppfb.FormId, invoice = invoice });
		}
		else
		{
			await Client.InvokeWithBusinessConnection(businessConnectionId,
				new Payments_TransferStarGift { stargift = stargift, to_id = peer });
		}
	}

	/// <summary>Posts a story on behalf of a managed business account. Requires the <em>CanManageStories</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="content">Content of the story</param>
	/// <param name="activePeriod">Period after which the story is moved to the archive, in seconds; must be one of <c>6 * 3600</c>, <c>12 * 3600</c>, <c>86400</c>, or <c>2 * 86400</c></param>
	/// <param name="caption">Caption of the story, 0-2048 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the story caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="areas">A list of clickable areas to be shown on the story</param>
	/// <param name="postToChatPage">Pass <see langword="true"/> to keep the story accessible after it expires</param>
	/// <param name="protectContent">Pass <see langword="true"/> if the content of the story must be protected from forwarding and screenshotting</param>
	/// <returns><see cref="Story"/> on success.</returns>
	public async Task<Story> PostStory(string businessConnectionId, InputStoryContent content, int activePeriod, string? caption = default,
		ParseMode parseMode = default, IEnumerable<MessageEntity>? captionEntities = default, IEnumerable<StoryArea>? areas = default,
		bool postToChatPage = default, bool protectContent = default)
	{
		//TODO: check why tdlib doesn't use InvokeWithBusinessConnection
		var entities = ApplyParse(parseMode, ref caption!, captionEntities);
		var peer = await GetBusinessPeer(businessConnectionId);
		var tlMedia = await GetStoryMedia(content);
		//tlMedia = (await Client.Messages_UploadMedia(peer, tlMedia)).ToInputMedia();
		var updates = await Client.Stories_SendStory(peer, tlMedia, [new InputPrivacyValueAllowAll()], Helpers.RandomLong(), caption, entities, activePeriod,
			areas?.Select(TypesTLConverters.MediaArea).ToArray(), pinned: postToChatPage, noforwards: protectContent);
		updates.UserOrChat(_collector);
		return new Story()
		{
			Chat = Chat(peer.ID)!,
			Id = updates.UpdateList.OfType<UpdateStoryID>().FirstOrDefault()?.id ?? 0
		};
	}

	/// <summary>Edits a story previously posted by the bot on behalf of a managed business account. Requires the <em>CanManageStories</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="storyId">Unique identifier of the story to edit</param>
	/// <param name="content">Content of the story</param>
	/// <param name="caption">Caption of the story, 0-2048 characters after entities parsing</param>
	/// <param name="parseMode">Mode for parsing entities in the story caption. See <a href="https://core.telegram.org/bots/api#formatting-options">formatting options</a> for more details.</param>
	/// <param name="captionEntities">A list of special entities that appear in the caption, which can be specified instead of <paramref name="parseMode"/></param>
	/// <param name="areas">A list of clickable areas to be shown on the story</param>
	/// <returns><see cref="Story"/> on success.</returns>
	public async Task<Story> EditStory(string businessConnectionId, int storyId, InputStoryContent content, string? caption = default,
		ParseMode parseMode = default, IEnumerable<MessageEntity>? captionEntities = default, IEnumerable<StoryArea>? areas = default)
	{
		var entities = ApplyParse(parseMode, ref caption!, captionEntities);
		var peer = await GetBusinessPeer(businessConnectionId);
		var tlMedia = await GetStoryMedia(content);
		//tlMedia = (await Client.Messages_UploadMedia(peer, tlMedia)).ToInputMedia();
		var updates = await Client.Stories_EditStory(peer, storyId, tlMedia, caption, entities, [new InputPrivacyValueAllowAll()],
			areas?.Select(TypesTLConverters.MediaArea).ToArray());
		updates.UserOrChat(_collector);
		return new Story()
		{
			Chat = Chat(peer.ID)!,
			Id = updates.UpdateList.OfType<UpdateStory>().FirstOrDefault()?.story.ID ?? 0
		};
	}

	/// <summary>Deletes a story previously posted by the bot on behalf of a managed business account. Requires the <em>CanManageStories</em> business bot right.</summary>
	/// <param name="businessConnectionId">Unique identifier of the business connection</param>
	/// <param name="storyId">Unique identifier of the story to delete</param>
	public async Task DeleteStory(string businessConnectionId, int storyId)
	{
		var peer = await GetBusinessPeer(businessConnectionId);
		await Client.InvokeWithBusinessConnection(businessConnectionId,
			new Stories_DeleteStories { peer = peer, id = [storyId] });
	}
	#endregion Updating messages

	#region Stickers

	/// <summary>Use this method to send static .WEBP, <a href="https://telegram.org/blog/animated-stickers">animated</a> .TGS, or <a href="https://telegram.org/blog/video-stickers-better-reactions">video</a> .WEBM stickers.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="sticker">Sticker to send. Pass a FileId as String to send a file that exists on the Telegram servers (recommended), pass an HTTP URL as a String for Telegram to get a .WEBP sticker from the Internet, or upload a new .WEBP, .TGS, or .WEBM sticker using <see cref="InputFileStream"/>. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a>. Video and animated stickers can't be sent via an HTTP URL.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">Additional interface options. An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>, <a href="https://core.telegram.org/bots/features#keyboards">custom reply keyboard</a>, instructions to remove a reply keyboard or to force a reply from the user</param>
	/// <param name="emoji">Emoji associated with the sticker; only for just uploaded stickers</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendSticker(ChatId chatId, InputFile sticker,
		ReplyParameters? replyParameters = default, ReplyMarkup? replyMarkup = default, string? emoji = default,
		int messageThreadId = 0, bool disableNotification = default, bool protectContent = default, long messageEffectId = 0,
		string? businessConnectionId = default, bool allowPaidBroadcast = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = await InputMediaDocument(sticker);
		if (media is TL.InputMediaUploadedDocument doc)
			doc.attributes = [.. doc.attributes ?? [], new DocumentAttributeSticker { alt = emoji }];
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, false),
			peer, null, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to get a sticker set.</summary>
	/// <param name="name">Name of the sticker set</param>
	/// <returns>A <see cref="StickerSet"/> object is returned.</returns>
	public async Task<Telegram.Bot.Types.StickerSet> GetStickerSet(string name)
	{
		await InitComplete();
		var mss = await Client.Messages_GetStickerSet(name);
		CacheStickerSet(mss);
		var thumb = mss.set.thumbs?[0].PhotoSize(mss.set.ToFileLocation(mss.set.thumbs[0]), mss.set.thumb_dc_id);
		var stickers = await mss.documents.OfType<TL.Document>().Select(async doc =>
		{
			var sticker = await MakeSticker(doc);
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
			Stickers = stickers,
			Thumbnail = thumb
		};
	}

	/// <summary>Use this method to get information about custom emoji stickers by their identifiers.</summary>
	/// <param name="customEmojiIds">A list of custom emoji identifiers. At most 200 custom emoji identifiers can be specified.</param>
	/// <returns>An Array of <see cref="Sticker"/> objects.</returns>
	public async Task<Sticker[]> GetCustomEmojiStickers(IEnumerable<string> customEmojiIds)
	{
		await InitComplete();
		var documents = await Client.Messages_GetCustomEmojiDocuments([.. customEmojiIds.Select(long.Parse)]);
		return await documents.OfType<TL.Document>().Select(async doc =>
		{
			var attrib = doc.GetAttribute<DocumentAttributeCustomEmoji>();
			return await MakeSticker(doc, null);
		}).WhenAllSequential();
	}

	/// <summary>Use this method to upload a file with a sticker for later use in the <see cref="WTelegram.Bot.CreateNewStickerSet">CreateNewStickerSet</see>, <see cref="WTelegram.Bot.AddStickerToSet">AddStickerToSet</see>, or <see cref="WTelegram.Bot.ReplaceStickerInSet">ReplaceStickerInSet</see> methods (the file can be used multiple times).</summary>
	/// <param name="userId">User identifier of sticker file owner</param>
	/// <param name="sticker">A file with the sticker in .WEBP, .PNG, .TGS, or .WEBM format. See <a href="https://core.telegram.org/stickers">https://core.telegram.org/stickers</a> for technical requirements. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a></param>
	/// <param name="stickerFormat">Format of the sticker, must be one of <see cref="StickerFormat.Static">Static</see>, <see cref="StickerFormat.Animated">Animated</see>, <see cref="StickerFormat.Video">Video</see></param>
	/// <returns>The uploaded <see cref="TGFile"/> on success.</returns>
	public async Task<TGFile> UploadStickerFile(long userId, InputFileStream sticker, StickerFormat stickerFormat)
	{
		await InitComplete();
		var mimeType = MimeType(stickerFormat);
		var peer = InputPeerUser(userId);
		var uploadedFile = await Client.UploadFileAsync(sticker.Content, sticker.FileName);
		DocumentAttribute[] attribs = stickerFormat == StickerFormat.Animated ? [new DocumentAttributeSticker { }] : [];
		var media = new TL.InputMediaUploadedDocument(uploadedFile, mimeType, attribs);
		var messageMedia = await Client.Messages_UploadMedia(peer, media);
		if (messageMedia is not MessageMediaDocument { document: TL.Document doc })
			throw new WTException("Unexpected UploadMedia result");
		var file = new TGFile { FileSize = doc.size }.SetFileIds(doc.ToFileLocation(), doc.dc_id);
		file.FilePath = file.FileId + '/' + sticker.FileName;
		return file;
	}

	/// <summary>Use this method to create a new sticker set owned by a user. The bot will be able to edit the sticker set thus created.</summary>
	/// <param name="userId">User identifier of created sticker set owner</param>
	/// <param name="name">Short name of sticker set, to be used in <c>t.me/addstickers/</c> URLs (e.g., <em>animals</em>). Can contain only English letters, digits and underscores. Must begin with a letter, can't contain consecutive underscores and must end in <c>"_by_&lt;BotUsername&gt;"</c>. <c>&lt;BotUsername&gt;</c> is case insensitive. 1-64 characters.</param>
	/// <param name="title">Sticker set title, 1-64 characters</param>
	/// <param name="stickers">A list of 1-50 initial stickers to be added to the sticker set</param>
	/// <param name="stickerType">Type of stickers in the set, pass <see cref="StickerType.Regular">Regular</see>, <see cref="StickerType.Mask">Mask</see>, or <see cref="StickerType.CustomEmoji">CustomEmoji</see>. By default, a regular sticker set is created.</param>
	/// <param name="needsRepainting">Pass <see langword="true"/> if stickers in the sticker set must be repainted to the color of text when used in messages, the accent color if used as emoji status, white on chat photos, or another appropriate color based on context; for custom emoji sticker sets only</param>
	public async Task CreateNewStickerSet(long userId, string name, string title, IEnumerable<InputSticker> stickers,
		StickerType? stickerType = default, bool needsRepainting = default)
	{
		var tlStickers = await Task.WhenAll(stickers.Select(sticker => InputStickerSetItem(userId, sticker)));
		var mss = await Client.Stickers_CreateStickerSet(InputPeerUser(userId), title, name, tlStickers, null, "bot" + BotId,
			stickerType == StickerType.Mask, stickerType == StickerType.CustomEmoji, needsRepainting);
		CacheStickerSet(mss);
	}

	/// <summary>Use this method to add a new sticker to a set created by the bot. Emoji sticker sets can have up to 200 stickers. Other sticker sets can have up to 120 stickers.</summary>
	/// <param name="userId">User identifier of sticker set owner</param>
	/// <param name="name">Sticker set name</param>
	/// <param name="sticker">An object with information about the added sticker. If exactly the same sticker had already been added to the set, then the set isn't changed.</param>
	public async Task AddStickerToSet(long userId, string name, InputSticker sticker)
	{
		var tlSticker = await InputStickerSetItem(userId, sticker);
		var mss = await Client.Stickers_AddStickerToSet(name, tlSticker);
		CacheStickerSet(mss);
	}

	/// <summary>Use this method to move a sticker in a set created by the bot to a specific position.</summary>
	/// <param name="sticker">File identifier of the sticker</param>
	/// <param name="position">New sticker position in the set, zero-based</param>
	public async Task SetStickerPositionInSet(InputFileId sticker, int position)
	{
		var inputDoc = await InputDocument(sticker.Id);
		await Client.Stickers_ChangeStickerPosition(inputDoc, position);
	}

	/// <summary>Use this method to delete a sticker from a set created by the bot.</summary>
	/// <param name="sticker">File identifier of the sticker</param>
	public async Task DeleteStickerFromSet(InputFileId sticker)
	{
		var inputDoc = await InputDocument(sticker.Id);
		await Client.Stickers_RemoveStickerFromSet(inputDoc);
	}

	/// <summary>Use this method to replace an existing sticker in a sticker set with a new one. The method is equivalent to calling <see cref="WTelegram.Bot.DeleteStickerFromSet">DeleteStickerFromSet</see>, then <see cref="WTelegram.Bot.AddStickerToSet">AddStickerToSet</see>, then <see cref="WTelegram.Bot.SetStickerPositionInSet">SetStickerPositionInSet</see>.</summary>
	/// <param name="userId">User identifier of the sticker set owner</param>
	/// <param name="name">Sticker set name</param>
	/// <param name="oldSticker">File identifier of the replaced sticker</param>
	/// <param name="sticker">An object with information about the added sticker. If exactly the same sticker had already been added to the set, then the set remains unchanged.</param>
	public async Task ReplaceStickerInSet(long userId, string name, string oldSticker, InputSticker sticker)
	{
		var inputDoc = await InputDocument(oldSticker);
		var tlSticker = await InputStickerSetItem(userId, sticker);
		var mss = await Client.Stickers_ReplaceSticker(inputDoc, tlSticker);
		CacheStickerSet(mss);
	}

	/// <summary>Use this method to change the list of emoji or the search keywords assigned to a regular or custom emoji sticker ; or to change the mask position of a mask sticker. The sticker must belong to a sticker set created by the bot.</summary>
	/// <param name="sticker"><see cref="InputFileId">File identifier</see> of the sticker</param>
	/// <param name="emojiList">(optional) A string composed of 1-20 emoji associated with the sticker</param>
	/// <param name="keywords">(optional) A comma-separated list of 0-20 search keywords for the sticker with total length of up to 64 characters. Pass an empty list to remove keywords.</param>
	/// <param name="maskPosition">(optional) An object with the position where the mask should be placed on faces. Pass null to remove the mask position.</param>
	public async Task SetStickerInfo(InputFileId sticker, string? emojiList = default, string? keywords = default,
		MaskPosition? maskPosition = default)
	{
		var inputDoc = await InputDocument(sticker.Id);
		await Client.Stickers_ChangeSticker(inputDoc, emojiList, maskPosition.MaskCoord(), keywords);
	}

	/// <summary>Use this method to set the title of a created sticker set.</summary>
	/// <param name="name">Sticker set name</param>
	/// <param name="title">Sticker set title, 1-64 characters</param>
	public async Task SetStickerSetTitle(string name, string title)
	{
		await InitComplete();
		await Client.Stickers_RenameStickerSet(name, title);
	}

	/// <summary>Use this method to set the thumbnail of a regular or mask sticker set. The format of the thumbnail file must match the format of the stickers in the set.</summary>
	/// <param name="name">Sticker set name</param>
	/// <param name="userId">User identifier of the sticker set owner</param>
	/// <param name="format">Format of the thumbnail, must be one of <see cref="StickerFormat.Static">Static</see> for a <b>.WEBP</b> or <b>.PNG</b> image, <see cref="StickerFormat.Animated">Animated</see> for a <b>.TGS</b> animation, or <see cref="StickerFormat.Video">Video</see> for a <b>.WEBM</b> video</param>
	/// <param name="thumbnail">A <b>.WEBP</b> or <b>.PNG</b> image with the thumbnail, must be up to 128 kilobytes in size and have a width and height of exactly 100px, or a <b>.TGS</b> animation with a thumbnail up to 32 kilobytes in size (see <a href="https://core.telegram.org/stickers#animation-requirements">https://core.telegram.org/stickers#animation-requirements</a> for animated sticker technical requirements), or a <b>.WEBM</b> video with the thumbnail up to 32 kilobytes in size; see <a href="https://core.telegram.org/stickers#video-requirements">https://core.telegram.org/stickers#video-requirements</a> for video sticker technical requirements. Pass a <em>FileId</em> as a String to send a file that already exists on the Telegram servers, pass an HTTP URL as a String for Telegram to get a file from the Internet, or upload a new one using <see cref="InputFileStream"/>. <a href="https://core.telegram.org/bots/api#sending-files">More information on Sending Files »</a>. Animated and video sticker set thumbnails can't be uploaded via HTTP URL. If omitted, then the thumbnail is dropped and the first sticker is used as the thumbnail.</param>
	public async Task SetStickerSetThumbnail(string name, long userId, StickerFormat format, InputFile? thumbnail = default)
	{
		await InitComplete();
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
	/// <param name="customEmojiId">Custom emoji identifier of a sticker from the sticker set; pass an empty string to drop the thumbnail and use the first sticker as the thumbnail.</param>
	public async Task SetCustomEmojiStickerSetThumbnail(string name, string? customEmojiId = default)
	{
		await InitComplete();
		await Client.Stickers_SetStickerSetThumb(name, null, customEmojiId == null ? null : long.Parse(customEmojiId));
	}

	/// <summary>Use this method to delete a sticker set that was created by the bot.</summary>
	/// <param name="name">Sticker set name</param>
	public async Task DeleteStickerSet(string name)
	{
		await InitComplete();
		await Client.Stickers_DeleteStickerSet(name);
	}
	#endregion

	#region Inline mode

	/// <summary>Use this method to send answers to an inline query<br/>No more than <b>50</b> results per query are allowed.</summary>
	/// <param name="inlineQueryId">Unique identifier for the answered query</param>
	/// <param name="results">A array of results for the inline query</param>
	/// <param name="cacheTime">The maximum amount of time in seconds that the result of the inline query may be cached on the server. Defaults to 300.</param>
	/// <param name="isPersonal">Pass <see langword="true"/> if results may be cached on the server side only for the user that sent the query. By default, results may be returned to any user who sends the same query.</param>
	/// <param name="nextOffset">Pass the offset that a client should send in the next query with the same text to receive more results. Pass an empty string if there are no more results or if you don't support pagination. Offset length can't exceed 64 bytes.</param>
	/// <param name="button">An object describing a button to be shown above inline query results</param>
	public async Task AnswerInlineQuery(string inlineQueryId, IEnumerable<InlineQueryResult> results, int cacheTime = 300,
		bool isPersonal = default, string? nextOffset = default, InlineQueryResultsButton? button = default)
	{
		await InitComplete();
		var switch_pm = button?.StartParameter == null ? null : new InlineBotSwitchPM { text = button.Text, start_param = button.StartParameter };
		var switch_webview = button?.WebApp == null ? null : new InlineBotWebView { text = button.Text, url = button.WebApp.Url };
		await Client.Messages_SetInlineBotResults(long.Parse(inlineQueryId), await InputBotInlineResults(results), cacheTime,
			nextOffset, switch_pm, switch_webview, private_: isPersonal);
	}

	/// <summary>Use this method to set the result of an interaction with a <a href="https://core.telegram.org/bots/webapps">Web App</a> and send a corresponding message on behalf of the user to the chat from which the query originated.</summary>
	/// <param name="webAppQueryId">Unique identifier for the query to be answered</param>
	/// <param name="result">An object describing the message to be sent</param>
	/// <returns>The sent inline message id if any</returns>
	public async Task<string?> AnswerWebAppQuery(string webAppQueryId, InlineQueryResult result)
	{
		await InitComplete();
		var sent = await Client.Messages_SendWebViewResultMessage(webAppQueryId, await InputBotInlineResult(result));
		return sent.msg_id.InlineMessageId();
	}

	#endregion Inline mode

	#region Payments

	/// <summary>Stores a message that can be sent by a user of a Mini App.</summary>
	/// <param name="userId">Unique identifier of the target user that can use the prepared message</param>
	/// <param name="result">An object describing the message to be sent</param>
	/// <param name="allowUserChats">Pass <see langword="true"/> if the message can be sent to private chats with users</param>
	/// <param name="allowBotChats">Pass <see langword="true"/> if the message can be sent to private chats with bots</param>
	/// <param name="allowGroupChats">Pass <see langword="true"/> if the message can be sent to group and supergroup chats</param>
	/// <param name="allowChannelChats">Pass <see langword="true"/> if the message can be sent to channel chats</param>
	/// <returns>A <see cref="PreparedInlineMessage"/> object.</returns>
	public async Task<PreparedInlineMessage> SavePreparedInlineMessage(long userId, InlineQueryResult result, bool allowUserChats = default,
		bool allowBotChats = default, bool allowGroupChats = default, bool allowChannelChats = default)
	{
		await InitComplete();
		var botResult = await InputBotInlineResult(result);
		var peer_types = TypesTLConverters.InlineQueryPeerTypes(allowUserChats, allowBotChats, allowGroupChats, allowChannelChats);
		var prepared = await Client.Messages_SavePreparedInlineMessage(botResult, InputUser(userId), peer_types);
		return new PreparedInlineMessage { Id = prepared.id, ExpirationDate = prepared.expire_date };
	}

	/// <summary>Use this method to send invoices.</summary>
	/// <param name="chatId">Unique identifier for the target chat or username of the target channel (in the format <c>@channelusername</c>)</param>
	/// <param name="title">Product name, 1-32 characters</param>
	/// <param name="description">Product description, 1-255 characters</param>
	/// <param name="payload">Bot-defined invoice payload, 1-128 bytes. This will not be displayed to the user, use it for your internal processes.</param>
	/// <param name="currency">Three-letter ISO 4217 currency code, see <a href="https://core.telegram.org/bots/payments#supported-currencies">more on currencies</a>. Pass “XTR” for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="prices">Price breakdown, a list of components (e.g. product price, tax, discount, delivery cost, delivery tax, bonus, etc.). Must contain exactly one item for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="providerToken">Payment provider token, obtained via <a href="https://t.me/botfather">@BotFather</a>. Pass an empty string for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="providerData">JSON-serialized data about the invoice, which will be shared with the payment provider. A detailed description of required fields should be provided by the payment provider.</param>
	/// <param name="maxTipAmount">The maximum accepted amount for tips in the <em>smallest units</em> of the currency (integer, <b>not</b> float/double). For example, for a maximum tip of <c>US$ 1.45</c> pass <c><paramref name="maxTipAmount"/> = 145</c>. See the <em>exp</em> parameter in <a href="https://core.telegram.org/bots/payments/currencies.json">currencies.json</a>, it shows the number of digits past the decimal point for each currency (2 for the majority of currencies). Defaults to 0. Not supported for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="suggestedTipAmounts">A array of suggested amounts of tips in the <em>smallest units</em> of the currency (integer, <b>not</b> float/double). At most 4 suggested tip amounts can be specified. The suggested tip amounts must be positive, passed in a strictly increased order and must not exceed <paramref name="maxTipAmount"/>.</param>
	/// <param name="photoUrl">URL of the product photo for the invoice. Can be a photo of the goods or a marketing image for a service. People like it better when they see what they are paying for.</param>
	/// <param name="photoSize">Photo size in bytes</param>
	/// <param name="photoWidth">Photo width</param>
	/// <param name="photoHeight">Photo height</param>
	/// <param name="needName">Pass <see langword="true"/> if you require the user's full name to complete the order. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="needPhoneNumber">Pass <see langword="true"/> if you require the user's phone number to complete the order. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="needEmail">Pass <see langword="true"/> if you require the user's email address to complete the order. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="needShippingAddress">Pass <see langword="true"/> if you require the user's shipping address to complete the order. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="sendPhoneNumberToProvider">Pass <see langword="true"/> if the user's phone number should be sent to the provider. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="sendEmailToProvider">Pass <see langword="true"/> if the user's email address should be sent to the provider. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="isFlexible">Pass <see langword="true"/> if the final price depends on the shipping method. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>. If empty, one 'Pay <c>total price</c>' button will be shown. If not empty, the first button must be a Pay button.</param>
	/// <param name="startParameter">Unique deep-linking parameter. If left empty, <b>forwarded copies</b> of the sent message will have a <em>Pay</em> button, allowing multiple users to pay directly from the forwarded message, using the same invoice. If non-empty, forwarded copies of the sent message will have a <em>URL</em> button with a deep link to the bot (instead of a <em>Pay</em> button), with the value used as the start parameter</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendInvoice(ChatId chatId, string title, string description, string payload, string currency,
		IEnumerable<LabeledPrice> prices, string? providerToken = default, string? providerData = default, int? maxTipAmount = default,
		IEnumerable<int>? suggestedTipAmounts = default, string? photoUrl = default, int? photoSize = default, int? photoWidth = default,
		int? photoHeight = default, bool needName = default, bool needPhoneNumber = default, bool needEmail = default,
		bool needShippingAddress = default, bool sendPhoneNumberToProvider = default, bool sendEmailToProvider = default,
		bool isFlexible = default, ReplyParameters? replyParameters = default, InlineKeyboardMarkup? replyMarkup = default,
		string? startParameter = default, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, bool allowPaidBroadcast = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = InputMediaInvoice(title, description, payload, providerToken, currency, prices, maxTipAmount, suggestedTipAmounts, startParameter,
			providerData, photoUrl, photoSize, photoWidth, photoHeight, needName, needPhoneNumber, needEmail, needShippingAddress,
			sendPhoneNumberToProvider, sendEmailToProvider, isFlexible, null);
		return await PostedMsg(Messages_SendMedia(null, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, false),
			peer, null, replyToMessage);
	}

	/// <summary>Use this method to create a link for an invoice.</summary>
	/// <param name="title">Product name, 1-32 characters</param>
	/// <param name="description">Product description, 1-255 characters</param>
	/// <param name="payload">Bot-defined invoice payload, 1-128 bytes. This will not be displayed to the user, use it for your internal processes.</param>
	/// <param name="currency">Three-letter ISO 4217 currency code, see <a href="https://core.telegram.org/bots/payments#supported-currencies">more on currencies</a>. Pass “XTR” for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="prices">Price breakdown, a list of components (e.g. product price, tax, discount, delivery cost, delivery tax, bonus, etc.). Must contain exactly one item for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="providerToken">Payment provider token, obtained via <a href="https://t.me/botfather">@BotFather</a>. Pass an empty string for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="providerData">JSON-serialized data about the invoice, which will be shared with the payment provider. A detailed description of required fields should be provided by the payment provider.</param>
	/// <param name="maxTipAmount">The maximum accepted amount for tips in the <em>smallest units</em> of the currency (integer, <b>not</b> float/double). For example, for a maximum tip of <c>US$ 1.45</c> pass <c><paramref name="maxTipAmount"/> = 145</c>. See the <em>exp</em> parameter in <a href="https://core.telegram.org/bots/payments/currencies.json">currencies.json</a>, it shows the number of digits past the decimal point for each currency (2 for the majority of currencies). Defaults to 0. Not supported for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="suggestedTipAmounts">A array of suggested amounts of tips in the <em>smallest units</em> of the currency (integer, <b>not</b> float/double). At most 4 suggested tip amounts can be specified. The suggested tip amounts must be positive, passed in a strictly increased order and must not exceed <paramref name="maxTipAmount"/>.</param>
	/// <param name="photoUrl">URL of the product photo for the invoice. Can be a photo of the goods or a marketing image for a service.</param>
	/// <param name="photoSize">Photo size in bytes</param>
	/// <param name="photoWidth">Photo width</param>
	/// <param name="photoHeight">Photo height</param>
	/// <param name="needName">Pass <see langword="true"/> if you require the user's full name to complete the order. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="needPhoneNumber">Pass <see langword="true"/> if you require the user's phone number to complete the order. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="needEmail">Pass <see langword="true"/> if you require the user's email address to complete the order. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="needShippingAddress">Pass <see langword="true"/> if you require the user's shipping address to complete the order. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="sendPhoneNumberToProvider">Pass <see langword="true"/> if the user's phone number should be sent to the provider. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="sendEmailToProvider">Pass <see langword="true"/> if the user's email address should be sent to the provider. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="isFlexible">Pass <see langword="true"/> if the final price depends on the shipping method. Ignored for payments in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</param>
	/// <param name="subscriptionPeriod">The number of seconds the subscription will be active for before the next payment. The currency must be set to “XTR” (Telegram Stars) if the parameter is used. Currently, it must always be 2592000 (30 days) if specified. Any number of subscriptions can be active for a given bot at the same time, including multiple concurrent subscriptions from the same user. Subscription price must no exceed 2500 Telegram Stars.</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the link will be created. For payments in <a href="https://t.me/BotNews/90">Telegram Stars</a> only.</param>
	/// <returns>The created invoice link as <em>String</em> on success.</returns>
	public async Task<string> CreateInvoiceLink(string title, string description, string payload, string currency,
		IEnumerable<LabeledPrice> prices, string? providerToken = default, string? providerData = default, int? maxTipAmount = default,
		IEnumerable<int>? suggestedTipAmounts = default, string? photoUrl = default, int? photoSize = default, int? photoWidth = default,
		int? photoHeight = default, bool needName = default, bool needPhoneNumber = default, bool needEmail = default,
		bool needShippingAddress = default, bool sendPhoneNumberToProvider = default, bool sendEmailToProvider = default,
		bool isFlexible = default, int? subscriptionPeriod = default, string? businessConnectionId = default)
	{
		await InitComplete();
		var query = new Payments_ExportInvoice
		{
			invoice_media =
			InputMediaInvoice(title, description, payload, providerToken, currency, prices, maxTipAmount, suggestedTipAmounts, null,
				providerData, photoUrl, photoSize, photoWidth, photoHeight, needName, needPhoneNumber, needEmail, needShippingAddress,
				sendPhoneNumberToProvider, sendEmailToProvider, isFlexible, subscriptionPeriod)
		};
		var exported = businessConnectionId is null ? await Client.Invoke(query)
			: await Client.InvokeWithBusinessConnection(businessConnectionId, query);
		return exported.url;
	}

	/// <summary>If you sent an invoice requesting a shipping address and the parameter <em>IsFlexible</em> was specified, the Bot API will send an <see cref="Update"/> with a <em>ShippingQuery</em> field to the bot. Use this method to reply to shipping queries</summary>
	/// <param name="shippingQueryId">Unique identifier for the query to be answered</param>
	/// <param name="shippingOptions">Required on success. A array of available shipping options.</param>
	/// <param name="errorMessage">Required on failure. Error message in human readable form that explains why it is impossible to complete the order (e.g. “Sorry, delivery to your desired address is unavailable”). Telegram will display this message to the user.</param>
	public async Task AnswerShippingQuery(string shippingQueryId, IEnumerable<ShippingOption>? shippingOptions = default,
		string? errorMessage = default)
	{
		await InitComplete();
		await Client.Messages_SetBotShippingResults(long.Parse(shippingQueryId), error: errorMessage, shipping_options:
			shippingOptions?.Select(so => new TL.ShippingOption { id = so.Id, title = so.Title, prices = so.Prices.LabeledPrices() }).ToArray());
	}

	/// <summary>Once the user has confirmed their payment and shipping details, the Bot API sends the final confirmation in the form of an <see cref="Update"/> with the field <em>PreCheckoutQuery</em>. Use this method to respond to such pre-checkout queries <b>Note:</b> The Bot API must receive an answer within 10 seconds after the pre-checkout query was sent.</summary>
	/// <param name="preCheckoutQueryId">Unique identifier for the query to be answered</param>
	/// <param name="errorMessage">Required on failure. Error message in human readable form that explains the reason for failure to proceed with the checkout (e.g. "Sorry, somebody just bought the last of our amazing black T-shirts while you were busy filling out your payment details. Please choose a different color or garment!"). Telegram will display this message to the user.</param>
	public async Task AnswerPreCheckoutQuery(string preCheckoutQueryId, string? errorMessage = default)
	{
		await InitComplete();
		await Client.Messages_SetBotPrecheckoutResults(long.Parse(preCheckoutQueryId), errorMessage, success: errorMessage == null);
	}

	/// <summary>Returns the bot's Telegram Star transactions in chronological order.</summary>
	/// <param name="offset">Number of transactions to skip in the response</param>
	/// <param name="limit">The maximum number of transactions to be retrieved. Values between 1-100 are accepted. Defaults to 100.</param>
	/// <returns>A <see cref="StarTransactions"/> object.</returns>
	public async Task<StarTransactions> GetStarTransactions(int offset = 0, int limit = 100)
	{
		await InitComplete();
		var starStatus = await Client.Payments_GetStarsTransactions(InputPeer.Self, offset.ToString(), limit, ascending: true);
		return new() { Transactions = [.. starStatus.history.Select(MakeStarTransaction)] };
	}

	/// <summary>Refunds a successful payment in <a href="https://t.me/BotNews/90">Telegram Stars</a>.</summary>
	/// <param name="userId">Identifier of the user whose payment will be refunded</param>
	/// <param name="telegramPaymentChargeId">Telegram payment identifier</param>
	public async Task RefundStarPayment(long userId, string telegramPaymentChargeId)
	{
		await InitComplete();
		await Client.Payments_RefundStarsCharge(InputUser(userId), telegramPaymentChargeId);
	}

	#endregion Payments

	#region Telegram Passport
	/// <summary>Allows the bot to cancel or re-enable extension of a subscription paid in Telegram Stars.</summary>
	/// <param name="userId">Identifier of the user whose subscription will be edited</param>
	/// <param name="telegramPaymentChargeId">Telegram payment identifier for the subscription</param>
	/// <param name="isCanceled">Pass <see langword="true"/> to cancel extension of the user subscription; the subscription must be active up to the end of the current subscription period. Pass <see langword="false"/> to allow the user to re-enable a subscription that was previously canceled by the bot.</param>
	public async Task EditUserStarSubscription(long userId, string telegramPaymentChargeId, bool isCanceled)
	{
		await InitComplete();
		await Client.Payments_BotCancelStarsSubscription(InputUser(userId), charge_id: telegramPaymentChargeId, restore: !isCanceled);
	}

	/// <summary>Informs a user that some of the Telegram Passport elements they provided contains errors. The user will not be able to re-submit their Passport to you until the errors are fixed (the contents of the field for which you returned the error must change).<br/>Use this if the data submitted by the user doesn't satisfy the standards your service requires for any reason. For example, if a birthday date seems invalid, a submitted document is blurry, a scan shows evidence of tampering, etc. Supply some details in the error message to make sure the user knows how to correct the issues. </summary>
	/// <param name="userId">User identifier</param>
	/// <param name="errors">A array describing the errors</param>
	public async Task SetPassportDataErrors(long userId, IEnumerable<PassportElementError> errors)
	{
		await InitComplete();
		var peer = InputPeerUser(userId);
		await Client.Users_SetSecureValueErrors(peer, [.. errors.Select(TypesTLConverters.SecureValueError)]);
	}
	#endregion Telegram Passport

	#region Games

	/// <summary>Use this method to send a game.</summary>
	/// <param name="chatId">Unique identifier for the target chat</param>
	/// <param name="gameShortName">Short name of the game, serves as the unique identifier for the game. Set up your games via <a href="https://t.me/botfather">@BotFather</a>.</param>
	/// <param name="replyParameters">Description of the message to reply to</param>
	/// <param name="replyMarkup">An object for an <a href="https://core.telegram.org/bots/features#inline-keyboards">inline keyboard</a>. If empty, one 'Play GameTitle' button will be shown. If not empty, the first button must launch the game.</param>
	/// <param name="messageThreadId">Unique identifier for the target message thread (topic) of the forum; for forum supergroups only</param>
	/// <param name="disableNotification">Sends the message <a href="https://telegram.org/blog/channels-2-0#silent-messages">silently</a>. Users will receive a notification with no sound.</param>
	/// <param name="protectContent">Protects the contents of the sent message from forwarding and saving</param>
	/// <param name="messageEffectId">Unique identifier of the message effect to be added to the message; for private chats only</param>
	/// <param name="businessConnectionId">Unique identifier of the business connection on behalf of which the message will be sent</param>
	/// <param name="allowPaidBroadcast">Pass <see langword="true"/> to allow up to 1000 messages per second, ignoring <a href="https://core.telegram.org/bots/faq#how-can-i-message-all-of-my-bot-39s-subscribers-at-once">broadcasting limits</a> for a fee of 0.1 Telegram Stars per message. The relevant Stars will be withdrawn from the bot's balance</param>
	/// <returns>The sent <see cref="Message"/> is returned.</returns>
	public async Task<Message> SendGame(long chatId, string gameShortName,
		ReplyParameters? replyParameters = default, InlineKeyboardMarkup? replyMarkup = default, int messageThreadId = 0,
		bool disableNotification = default, bool protectContent = default, long messageEffectId = 0, string? businessConnectionId = default,
		bool allowPaidBroadcast = default)
	{
		var peer = await InputPeerChat(chatId);
		var replyToMessage = await GetReplyToMessage(peer, replyParameters);
		var reply_to = await MakeReplyTo(replyParameters, messageThreadId, peer);
		var media = new InputMediaGame { id = new InputGameShortName { bot_id = TL.InputUser.Self, short_name = gameShortName } };
		return await PostedMsg(Messages_SendMedia(businessConnectionId, peer, media, null, Helpers.RandomLong(), reply_to,
			await MakeReplyMarkup(replyMarkup), null, messageEffectId, disableNotification, protectContent, allowPaidBroadcast, false),
			peer, null, replyToMessage, businessConnectionId);
	}

	/// <summary>Use this method to set the score of the specified user in a game message.</summary>
	/// <remarks>Returns an error, if the new score is not greater than the user's current score in the chat and <paramref name="force"/> is <em>False</em>.</remarks>
	/// <param name="userId">User identifier</param>
	/// <param name="score">New score, must be non-negative</param>
	/// <param name="chatId">Unique identifier for the target chat</param>
	/// <param name="messageId">Identifier of the sent message</param>
	/// <param name="force">Pass <see langword="true"/> if the high score is allowed to decrease. This can be useful when fixing mistakes or banning cheaters</param>
	/// <param name="disableEditMessage">Pass <see langword="true"/> if the game message should not be automatically edited to include the current scoreboard</param>
	/// <returns>The <see cref="Message"/> is returned</returns>
	public async Task<Message> SetGameScore(long userId, int score, long chatId, int messageId, bool force = default,
		bool disableEditMessage = default)
	{
		var peer = await InputPeerChat(chatId);
		var updates = await Client.Messages_SetGameScore(peer, messageId, InputUser(userId), score, disableEditMessage != true, force);
		updates.UserOrChat(_collector);
		var editUpdate = updates.UpdateList.OfType<UpdateEditMessage>().FirstOrDefault(uem => uem.message.Peer.ID == peer.ID && uem.message.ID == messageId);
		if (editUpdate != null) return (await MakeMessage(editUpdate.message))!;
		else return await PostedMsg(Task.FromResult(updates), peer);
	}

	/// <summary>Use this method to set the score of the specified user in a game message.</summary>
	/// <remarks>Returns an error, if the new score is not greater than the user's current score in the chat and <paramref name="force"/> is <em>False</em>.</remarks>
	/// <param name="userId">User identifier</param>
	/// <param name="score">New score, must be non-negative</param>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <param name="force">Pass <see langword="true"/> if the high score is allowed to decrease. This can be useful when fixing mistakes or banning cheaters</param>
	/// <param name="disableEditMessage">Pass <see langword="true"/> if the game message should not be automatically edited to include the current scoreboard</param>
	public async Task SetGameScore(long userId, int score, string inlineMessageId, bool force = default, bool disableEditMessage = default)
	{
		var id = await ParseInlineMsgID(inlineMessageId);
		await Client.Messages_SetInlineGameScore(id, InputUser(userId), score, disableEditMessage != true, force);
	}

	/// <summary>Use this method to get data for high score tables. Will return the score of the specified user and several of their neighbors in a game.</summary>
	/// <remarks>This method will currently return scores for the target user, plus two of their closest neighbors on each side. Will also return the top three users if the user and their neighbors are not among them. Please note that this behavior is subject to change.</remarks>
	/// <param name="userId">Target user id</param>
	/// <param name="chatId">Unique identifier for the target chat</param>
	/// <param name="messageId">Identifier of the sent message</param>
	/// <returns>An Array of <see cref="GameHighScore"/> objects.</returns>
	public async Task<GameHighScore[]> GetGameHighScores(long userId, long chatId, int messageId)
	{
		var peer = await InputPeerChat(chatId);
		var highScore = await Client.Messages_GetGameHighScores(peer, messageId, InputUser(userId));
		_collector.Collect(highScore.users.Values);
		return await Task.WhenAll(highScore.scores.Select(async hs => new GameHighScore
		{ Position = hs.pos, User = await UserOrResolve(hs.user_id), Score = hs.score }));
	}

	/// <summary>Use this method to get data for high score tables. Will return the score of the specified user and several of their neighbors in a game.</summary>
	/// <remarks>This method will currently return scores for the target user, plus two of their closest neighbors on each side. Will also return the top three users if the user and their neighbors are not among them. Please note that this behavior is subject to change.</remarks>
	/// <param name="userId">Target user id</param>
	/// <param name="inlineMessageId">Identifier of the inline message</param>
	/// <returns>An Array of <see cref="GameHighScore"/> objects.</returns>
	public async Task<GameHighScore[]> GetGameHighScores(long userId, string inlineMessageId)
	{
		var id = await ParseInlineMsgID(inlineMessageId);
		var highScore = await Client.Messages_GetInlineGameHighScores(id, InputUser(userId));
		_collector.Collect(highScore.users.Values);
		return await Task.WhenAll(highScore.scores.Select(async hs => new GameHighScore
		{ Position = hs.pos, User = await UserOrResolve(hs.user_id), Score = hs.score }));
	}
	#endregion Games
}
