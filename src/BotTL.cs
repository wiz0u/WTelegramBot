using TL;
using Message = WTelegram.Types.Message;
using MessageEntity = Telegram.Bot.Types.MessageEntity;

namespace WTelegram;

public partial class Bot
{
	/// <summary>Fetch the message being replied-to if any</summary>
	protected async Task<Message?> GetReplyToMessage(InputPeer peer, ReplyParameters? replied)
	{
		if (replied?.MessageId > 0)
		{
			if (replied.ChatId is not null) peer = await InputPeerChat(replied.ChatId, allowUsersName: true);
			var msg = await GetMessage(peer, replied.MessageId.Value);
			if (msg == null && !replied.AllowSendingWithoutReply) throw new WTException("Bad Request: message to reply not found");
			return msg;
		}
		return null;
	}

	/// <summary>Build the eventual InputReplyTo structure for sending a reply message</summary>
	protected async Task<InputReplyTo?> MakeReplyTo(ReplyParameters? replied, InputPeer? replyToPeer, int messageThreadId, long directMessagesTopicId = 0)
	{
		if (replied?.MessageId > 0)
		{
			if (replied.ChatId is null)
				replyToPeer = null;
			else
			{
				var targetPeerId = replyToPeer?.ID;
				replyToPeer = await InputPeerChat(replied.ChatId, allowUsersName: true);
				if (replyToPeer.ID == targetPeerId) replyToPeer = null;
			}
			var monoforum_peer_id = directMessagesTopicId != 0 ? InputPeerUser(directMessagesTopicId) : null;

			var quote = replied.Quote;
			var quoteEntities = ApplyParse(replied.QuoteParseMode, ref quote, replied.QuoteEntities);
			return new InputReplyToMessage
			{
				reply_to_msg_id = replied.MessageId.Value,
				top_msg_id = messageThreadId,
				reply_to_peer_id = replyToPeer,
				quote_text = quote,
				quote_entities = quoteEntities,
				quote_offset = replied.QuotePosition ?? 0,
				todo_item_id = replied.ChecklistTaskId ?? 0,
				poll_option = replied.PollOptionId,
				monoforum_peer_id = monoforum_peer_id,
				flags = (messageThreadId != 0 ? InputReplyToMessage.Flags.has_top_msg_id : 0)
					| (replyToPeer is not null ? InputReplyToMessage.Flags.has_reply_to_peer_id : 0)
					| (quote != null ? InputReplyToMessage.Flags.has_quote_text : 0)
					| (quoteEntities != null ? InputReplyToMessage.Flags.has_quote_entities : 0)
					| (replied.QuotePosition.HasValue ? InputReplyToMessage.Flags.has_quote_offset : 0)
					| (replied.ChecklistTaskId.HasValue ? InputReplyToMessage.Flags.has_todo_item_id : 0)
					| (replied.PollOptionId != null ? InputReplyToMessage.Flags.has_poll_option : 0)
					| (monoforum_peer_id != null ? InputReplyToMessage.Flags.has_monoforum_peer_id : 0)
			};
		}
		else if (replied?.EphemeralMessageId > 0)
			return new InputReplyToEphemeralMessage { id = replied.EphemeralMessageId.Value };
		else if (messageThreadId != 0)
			return new InputReplyToMessage { reply_to_msg_id = messageThreadId };
		else if (directMessagesTopicId != 0)
			return new InputReplyToMonoForum { monoforum_peer_id = InputPeerUser(directMessagesTopicId) };
		return null;
	}

	private async Task<Message?> GetMIMessage(InputPeer peer, int messageId, bool replyToo = false)
	{
		var msg = await GetMessage(peer, messageId, replyToo: replyToo);
		if (msg != null && msg.Date != default) return msg;
		return new Message { Chat = await ChatFromPeer(peer), Id = messageId }; // Inaccessible Message
	}

	private async Task<Message?> GetRepliedMessage(MessageBase msg, bool canUseCache = false)
	{
		if (msg is not { ReplyHeader: { } mrh, Peer: var peer }) return null;
		var repliedPeer = mrh.reply_to_peer_id ?? peer;
		var repliedMsgId = Math.Max(mrh.reply_to_msg_id, mrh.reply_to_top_id);
		if (mrh.flags.HasFlag(MessageReplyHeader.Flags.reply_to_ephemeral)) repliedMsgId = -repliedMsgId;
		if (repliedPeer == null || repliedMsgId == 0) return null;
		if (repliedPeer != peer)
			return await GetMessage(await ChatFromPeer(repliedPeer, true), repliedMsgId, canUseCache: canUseCache);
		if (canUseCache)
			lock (CachedMessages)
				if (CachedMessages.TryGetValue((repliedPeer.ID, repliedMsgId), out var cachedMsg))
					return cachedMsg;
		if (repliedMsgId < 0) return null;
		var chat = await ChatFromPeer(peer, true);
		var msgs = await Client.GetMessages(chat, new InputMessageReplyTo { id = msg.ID });
		msgs.UserOrChat(_collector);
		var msgBase = msgs.Messages.FirstOrDefault();
		return await MakeMessage(msgBase);
	}

	/// <summary>Fetch and build a Bot Message (cached)</summary>
	protected async Task<Message?> GetMessage(InputPeer? peer, int messageId, bool replyToo = false, bool canUseCache = true)
	{
		if (peer == null || messageId == 0) return null;
		if (canUseCache)
			lock (CachedMessages)
				if (CachedMessages.TryGetValue((peer.ID, messageId), out var cachedMsg))
					return cachedMsg;
		if (messageId < 0) return null;
		var msgs = await Client.GetMessages(peer, messageId);
		msgs.UserOrChat(_collector);
		var msgBase = msgs.Messages.FirstOrDefault();
		return replyToo ? await MakeMessageAndReply(msgBase) : await MakeMessage(msgBase);
	}

	private Message? CacheMessage(Message? msg, MessageBase msgBase)
	{
		//TODO: remove old messages from cache after a configurable time
		if (msgBase.Peer is { ID: long peerId })
			lock (CachedMessages)
				CachedMessages[(peerId, msgBase.ID)] = msg;
		return msg;
	}

	private MessageEntity[]? MakeEntities(TL.MessageEntity[]? entities) => entities?.Select(e => e switch
	{
		TL.MessageEntityMention => new MessageEntity { Type = MessageEntityType.Mention, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityHashtag => new MessageEntity { Type = MessageEntityType.Hashtag, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityBotCommand => new MessageEntity { Type = MessageEntityType.BotCommand, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityUrl => new MessageEntity { Type = MessageEntityType.Url, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityEmail => new MessageEntity { Type = MessageEntityType.Email, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityBold => new MessageEntity { Type = MessageEntityType.Bold, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityItalic => new MessageEntity { Type = MessageEntityType.Italic, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityCode => new MessageEntity { Type = MessageEntityType.Code, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityPre mep => new MessageEntity { Type = MessageEntityType.Pre, Offset = e.Offset, Length = e.Length, Language = mep.language },
		TL.MessageEntityTextUrl metu => new MessageEntity { Type = MessageEntityType.TextLink, Offset = e.Offset, Length = e.Length, Url = metu.url },
		TL.MessageEntityMentionName memn => new MessageEntity { Type = MessageEntityType.TextMention, Offset = e.Offset, Length = e.Length, User = User(memn.user_id) },
		TL.MessageEntityPhone => new MessageEntity { Type = MessageEntityType.PhoneNumber, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityCashtag => new MessageEntity { Type = MessageEntityType.Cashtag, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityUnderline => new MessageEntity { Type = MessageEntityType.Underline, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityStrike => new MessageEntity { Type = MessageEntityType.Strikethrough, Offset = e.Offset, Length = e.Length },
		TL.MessageEntitySpoiler => new MessageEntity { Type = MessageEntityType.Spoiler, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityCustomEmoji mece => new MessageEntity { Type = MessageEntityType.CustomEmoji, Offset = e.Offset, Length = e.Length, CustomEmojiId = mece.document_id.ToString() },
		TL.MessageEntityBlockquote mebq => mebq.flags.HasFlag(MessageEntityBlockquote.Flags.collapsed)
			? new MessageEntity { Type = MessageEntityType.ExpandableBlockquote, Offset = e.Offset, Length = e.Length }
			: new MessageEntity { Type = MessageEntityType.Blockquote, Offset = e.Offset, Length = e.Length },
		TL.MessageEntityFormattedDate mefd => new MessageEntity { Type = MessageEntityType.DateTime, Offset = e.Offset, Length = e.Length, UnixTime = mefd.date, DateTimeFormat = mefd.flags.ToDateFormat() },
		_ => null!,
	}).Where(e => e != null).ToArray();

	/// <summary>Apply ParseMode to text and entities</summary>
	protected TL.MessageEntity[]? ApplyParse(ParseMode parseMode, ref string? text, IEnumerable<MessageEntity>? entities)
	{
		if (entities != null)
			return [.. entities.Select(e => e.Type switch
			{
				MessageEntityType.Bold => new TL.MessageEntityBold { offset = e.Offset, length = e.Length },
				MessageEntityType.Italic => new TL.MessageEntityItalic { offset = e.Offset, length = e.Length },
				MessageEntityType.Code => new TL.MessageEntityCode { offset = e.Offset, length = e.Length },
				MessageEntityType.Pre => new TL.MessageEntityPre { offset = e.Offset, length = e.Length, language = e.Language },
				MessageEntityType.TextLink => new TL.MessageEntityTextUrl { offset = e.Offset, length = e.Length, url = e.Url },
				MessageEntityType.TextMention => new TL.InputMessageEntityMentionName { offset = e.Offset, length = e.Length, user_id = InputUser(e.User!.Id) },
				MessageEntityType.Underline => new TL.MessageEntityUnderline { offset = e.Offset, length = e.Length },
				MessageEntityType.Strikethrough => new TL.MessageEntityStrike { offset = e.Offset, length = e.Length },
				MessageEntityType.Spoiler => new TL.MessageEntitySpoiler { offset = e.Offset, length = e.Length },
				MessageEntityType.CustomEmoji => new TL.MessageEntityCustomEmoji { offset = e.Offset, length = e.Length, document_id = long.Parse(e.CustomEmojiId!) },
				MessageEntityType.Blockquote => new TL.MessageEntityBlockquote { offset = e.Offset, length = e.Length },
				MessageEntityType.ExpandableBlockquote => new TL.MessageEntityBlockquote { offset = e.Offset, length = e.Length, flags = MessageEntityBlockquote.Flags.collapsed },
				MessageEntityType.DateTime => new TL.MessageEntityFormattedDate { offset = e.Offset, length = e.Length, flags = TL.HtmlText.ToDateFlags(e.DateTimeFormat ?? ""), date = e.UnixTime!.Value },
				_ => (TL.MessageEntity)null!
			}).Where(e => e != null)];
		else if (text == null)
			return null;
		return parseMode switch
		{
			ParseMode.Markdown or ParseMode.MarkdownV2 => Client.MarkdownToEntities(ref text, _collector),
			ParseMode.Html => Client.HtmlToEntities(ref text, _collector),
			_ => null,
		};
	}

	/// <summary>Apply ParseMode to text and entities</summary>
	protected string? ApplyParse(ParseMode parseMode, string? text, MessageEntity[]? entities, out TL.MessageEntity[]? tlEntities)
	{
		tlEntities = ApplyParse(parseMode, ref text, entities);
		return text;
	}

	private async Task<TL.BotCommandScope?> BotCommandScope(BotCommandScope? scope)
	{
		await InitComplete();
		return scope switch
		{
			BotCommandScopeAllPrivateChats => new BotCommandScopeUsers(),
			BotCommandScopeAllGroupChats => new BotCommandScopeChats(),
			BotCommandScopeAllChatAdministrators => new BotCommandScopeChatAdmins(),
			BotCommandScopeChat bcsc => new BotCommandScopePeer { peer = await InputPeerChat(bcsc.ChatId) },
			BotCommandScopeChatAdministrators bcsca => new BotCommandScopePeerAdmins { peer = await InputPeerChat(bcsca.ChatId) },
			BotCommandScopeChatMember bcscm => new BotCommandScopePeerUser { peer = await InputPeerChat(bcscm.ChatId), user_id = InputUser(bcscm.UserId) },
			_ => null
		};
	}

	private static InputGeoPoint MakeGeoPoint(double latitude, double longitude, double? horizontalAccuracy)
		=> MakeGeoPoint(latitude, longitude, horizontalAccuracy.HasValue ? (int)horizontalAccuracy.Value : 0);
	private static InputGeoPoint MakeGeoPoint(double latitude, double longitude, int horizontalAccuracy) => new()
	{
		lat = latitude,
		lon = longitude,
		accuracy_radius = horizontalAccuracy,
		flags = horizontalAccuracy > 0 ? InputGeoPoint.Flags.has_accuracy_radius : 0
	};

	private static InputMediaGeoLive MakeGeoLive(double latitude, double longitude, int horizontalAccuracy,
		int heading, int proximityAlertRadius, int livePeriod = 0) => new()
	{
		geo_point = MakeGeoPoint(latitude, longitude, horizontalAccuracy),
		period = livePeriod,
		heading = heading,
		proximity_notification_radius = proximityAlertRadius,
		flags = (livePeriod > 0 ? InputMediaGeoLive.Flags.has_period : 0)
			| (heading > 0 ? InputMediaGeoLive.Flags.has_heading : 0)
			| (proximityAlertRadius > 0 ? InputMediaGeoLive.Flags.has_proximity_notification_radius : 0)
	};

	/// <summary>Handle UpdatesBase returned by various Client API and build the returned Bot Message</summary>
	protected async Task<Message> PostedMsg(Task<UpdatesBase> updatesTask, InputPeer peer, string? text = null, Message? replyToMessage = null, ReplyMarkup? replyMarkup = null, string? bConnId = null)
	{
		var updates = await updatesTask;
		updates.UserOrChat(_collector);
		if (updates is UpdateShortSentMessage sent)
			return await FillTextAndMedia(new Message
			{
				Id = sent.id,
				From = await UserOrResolve(BotId),
				Date = sent.date,
				Chat = await ChatFromPeer(peer)!,
				ReplyToMessage = replyToMessage,
				ReplyMarkup = replyMarkup as InlineKeyboardMarkup
			}, text, sent.entities, sent.media);
		foreach (var update in updates.UpdateList)
		{
			switch (update)
			{
				case UpdateNewMessage { message: { } message }: return (await MakeMessageAndReply(message, replyToMessage))!;
				case UpdateNewScheduledMessage { message: { } schedMsg }: return (await MakeMessageAndReply(schedMsg, replyToMessage))!;
				case UpdateEditMessage { message: { } editMsg }: return (await MakeMessageAndReply(editMsg, replyToMessage))!;
				case UpdateBotNewBusinessMessage { message: { } bizMsg }: return (await MakeMessageAndReply(bizMsg, replyToMessage, bConnId))!;
				case UpdateNewEphemeralMessage { message: { } message }: return (await MakeEphemeralAndReply(message, replyToMessage))!;
			}
		}
		throw new WTException("Failed to retrieve sent message");
	}

	private async Task<Message[]> PostedMsgs(Task<UpdatesBase> updatesTask, int nbMsg, long startRandomId, Message? replyToMessage, string? bConnId = null)
	{
		var updates = await updatesTask;
		updates.UserOrChat(_collector);
		var result = new List<Message>(nbMsg);
		foreach (var update in updates.UpdateList)
		{
			Message? msg = null;
			switch (update)
			{
				case UpdateNewMessage { message: TL.Message message }: msg = await MakeMessageAndReply(message, replyToMessage); break;
				case UpdateNewScheduledMessage { message: TL.Message schedMsg }: msg = await MakeMessageAndReply(schedMsg, replyToMessage); break;
				case UpdateBotNewBusinessMessage { message: { } bizMsg } biz: msg = await MakeMessageAndReply(bizMsg, replyToMessage, bConnId); break;
			}
			if (msg != null) result.Add(msg);
		}
		return [.. result.OrderBy(msg => msg.MessageId)];
	}
	
	Task<UpdatesBase> Messages_SendMessage(string? bConnId, InputPeer peer, string? message, long random_id,
		InputReplyTo? reply_to, TL.ReplyMarkup? reply_markup, TL.MessageEntity[]? entities, InputRichMessageBase? rich_message, long effect, SuggestedPostParameters? suggested_post,
		long? receiverUserId, string? callbackQueryId, bool silent, bool noforwards, bool allow_paid_floodskip, bool invert_media, bool no_webpage)
	{
		IMethod<UpdatesBase> query;
		if (receiverUserId.HasValue)
			query = new TL.Methods.Ephemeral_SendMessage
			{
				flags = (TL.Methods.Ephemeral_SendMessage.Flags)((callbackQueryId != null ? 0x1 : 0) | (entities != null ? 0x2 : 0) 
					| (reply_markup != null ? 0x8 : 0) | (rich_message != null ? 0x10 : 0) | (reply_to != null ? 0x20 : 0)),
				//	| (no_webpage ? 0x2 : 0) | (silent ? 0x20 : 0) | (noforwards ? 0x4000 : 0) | (invert_media ? 0x10000 : 0) | (effect > 0 ? 0x40000 : 0)
				//	| (allow_paid_floodskip ? 0x80000 : 0) | (suggested_post != null ? 0x400000 : 0) | ),
				peer = peer,
				receiver_id = InputUser(receiverUserId.Value),
				query_id = callbackQueryId.LongOrDefault(),
				message = message,
				entities = entities,
				reply_markup = reply_markup,
				rich_message = rich_message,
				random_id = random_id,
				reply_to = reply_to,
			};
		else
			query = new TL.Methods.Messages_SendMessage
			{
				flags = (TL.Methods.Messages_SendMessage.Flags)((reply_to != null ? 0x1 : 0) | (reply_markup != null ? 0x4 : 0) | (entities != null ? 0x8 : 0)
					| (no_webpage ? 0x2 : 0) | (silent ? 0x20 : 0) | (noforwards ? 0x4000 : 0) | (invert_media ? 0x10000 : 0) | (effect > 0 ? 0x40000 : 0)
					| (allow_paid_floodskip ? 0x80000 : 0) | (suggested_post != null ? 0x400000 : 0) | (rich_message != null ? 0x800000 : 0)),
				peer = peer,
				reply_to = reply_to,
				message = message,
				random_id = random_id,
				reply_markup = reply_markup,
				entities = entities,
				effect = effect,
				suggested_post = suggested_post.SuggestedPost(),
				rich_message = rich_message,
			};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	Task<UpdatesBase> Messages_SendMedia(string? bConnId, InputPeer peer, TL.InputMedia media, string? message, long random_id,
		InputReplyTo? reply_to, TL.ReplyMarkup? reply_markup, TL.MessageEntity[]? entities, long effect, SuggestedPostParameters? suggested_post,
		long? receiverUserId, string? callbackQueryId, bool silent, bool noforwards, bool allow_paid_floodskip, bool invert_media)
	{
		IMethod<UpdatesBase> query;
		if (receiverUserId.HasValue)
			query = new TL.Methods.Ephemeral_SendMessage
			{
				flags = (TL.Methods.Ephemeral_SendMessage.Flags)((callbackQueryId != null ? 0x1 : 0) | (entities != null ? 0x2 : 0) | (media != null ? 0x4 : 0)
					| (reply_markup != null ? 0x8 : 0) | (reply_to != null ? 0x20 : 0)),
				//	| (silent ? 0x20 : 0) | (noforwards ? 0x4000 : 0) | (invert_media ? 0x10000 : 0) | (effect > 0 ? 0x40000 : 0)
				//	| (allow_paid_floodskip ? 0x80000 : 0) | (suggested_post != null ? 0x400000 : 0)),
				peer = peer,
				receiver_id = InputUser(receiverUserId.Value),
				query_id = callbackQueryId.LongOrDefault(),
				message = message,
				entities = entities,
				media = media,
				reply_markup = reply_markup,
				random_id = random_id,
				reply_to = reply_to,
			};
		else
			query = new TL.Methods.Messages_SendMedia
			{
				flags = (TL.Methods.Messages_SendMedia.Flags)((reply_to != null ? 0x1 : 0) | (reply_markup != null ? 0x4 : 0) | (entities != null ? 0x8 : 0)
					| (silent ? 0x20 : 0) | (noforwards ? 0x4000 : 0) | (invert_media ? 0x10000 : 0) | (effect > 0 ? 0x40000 : 0)
					| (allow_paid_floodskip ? 0x80000 : 0) | (suggested_post != null ? 0x400000 : 0)),
				peer = peer,
				reply_to = reply_to,
				media = media,
				message = message,
				random_id = random_id,
				reply_markup = reply_markup,
				entities = entities,
				effect = effect,
				suggested_post = suggested_post.SuggestedPost(),
			};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	Task<UpdatesBase> Messages_SendMultiMedia(string? bConnId, InputPeer peer, InputSingleMedia[] multi_media,
		InputReplyTo? reply_to, long effect, bool silent, bool noforwards, bool allow_paid_floodskip, bool invert_media)
	{
		var query = new TL.Methods.Messages_SendMultiMedia
		{
			flags = (TL.Methods.Messages_SendMultiMedia.Flags)((reply_to != null ? 0x1 : 0) | (silent ? 0x20 : 0) | (noforwards ? 0x4000 : 0) | (invert_media ? 0x10000 : 0) | (effect > 0 ? 0x40000 : 0) | (allow_paid_floodskip ? 0x80000 : 0)),
			peer = peer,
			reply_to = reply_to,
			multi_media = multi_media,
			effect = effect
		};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	Task<UpdatesBase> Messages_EditMessage(string? bConnId, InputPeer peer, int id, string? message = null, TL.InputMedia? media = null, TL.ReplyMarkup? reply_markup = null, TL.MessageEntity[]? entities = null, TL.InputRichMessageBase? rich_message = null, DateTime? schedule_date = null, int? quick_reply_shortcut_id = null, bool no_webpage = false, bool invert_media = false)
	{
		var query = new TL.Methods.Messages_EditMessage
		{
			flags = (TL.Methods.Messages_EditMessage.Flags)((message != null ? 0x800 : 0) | (media != null ? 0x4000 : 0) | (reply_markup != null ? 0x4 : 0)
				| (entities != null ? 0x8 : 0) | (rich_message != null ? 0x800000 : 0) | (schedule_date != null ? 0x8000 : 0)
				| (quick_reply_shortcut_id != null ? 0x20000 : 0) | (no_webpage ? 0x2 : 0) | (invert_media ? 0x10000 : 0)),
			peer = peer,
			id = id,
			message = message,
			media = media,
			reply_markup = reply_markup,
			entities = entities,
			rich_message = rich_message,
			schedule_date = schedule_date.GetValueOrDefault(),
			quick_reply_shortcut_id = quick_reply_shortcut_id.GetValueOrDefault(),
		};
		return bConnId is null ? Client.Invoke(query) : Client.InvokeWithBusinessConnection(bConnId, query);
	}

	async Task<bool> Messages_EditInlineBotMessage(string? bConnId, InputBotInlineMessageIDBase id, string? message = null, TL.InputMedia? media = null, TL.ReplyMarkup? reply_markup = null, TL.MessageEntity[]? entities = null, TL.InputRichMessageBase? rich_message = null, bool no_webpage = false, bool invert_media = false)
	{
		var query = new TL.Methods.Messages_EditInlineBotMessage
		{
			flags = (TL.Methods.Messages_EditInlineBotMessage.Flags)((message != null ? 0x800 : 0) | (media != null ? 0x4000 : 0) | (reply_markup != null ? 0x4 : 0)
				| (entities != null ? 0x8 : 0) | (rich_message != null ? 0x800000 : 0) | (no_webpage ? 0x2 : 0) | (invert_media ? 0x10000 : 0)),
			id = id,
			message = message,
			media = media,
			reply_markup = reply_markup,
			entities = entities,
			rich_message = rich_message,
		};
		var dcClient = await Client.GetClientForDC(id.DcId);
		return bConnId is null ? await dcClient.Invoke(query) : await dcClient.InvokeWithBusinessConnection(bConnId, query);
	}

	private async Task<ChatBoost> MakeBoost(Boost boost)
	{
		var cb = new ChatBoost
		{
			BoostId = boost.id,
			AddDate = boost.date,
			ExpirationDate = boost.expires,
		};
		if (boost.flags.HasFlag(Boost.Flags.giveaway))
			cb.Source = new ChatBoostSourceGiveaway
			{
				GiveawayMessageId = boost.giveaway_msg_id,
				User = boost.user_id == 0 ? null : await UserOrResolve(boost.user_id),
				PrizeStarCount = ((int)boost.stars).NullIfZero(),
				IsUnclaimed = boost.flags.HasFlag(Boost.Flags.unclaimed)
			};
		else if (boost.flags.HasFlag(Boost.Flags.gift))
			cb.Source = new ChatBoostSourceGiftCode { User = await UserOrResolve(boost.user_id) };
		else
			cb.Source = new ChatBoostSourcePremium { User = await UserOrResolve(boost.user_id) };
		return cb;
	}

	internal async Task<Telegram.Bot.Types.BusinessIntro?> MakeBusinessIntro(TL.BusinessIntro? intro) => intro == null ? null : new()
	{
		Title = intro.title,
		Message = intro.description,
		Sticker = intro.sticker is TL.Document doc ? await MakeSticker(doc) : null
	};

	private async Task<BusinessConnection> MakeBusinessConnection(BotBusinessConnection bbc) => new()
	{
		Id = bbc.connection_id,
		User = await UserOrResolve(bbc.user_id),
		UserChatId = bbc.user_id,
		Date = bbc.date,
		Rights = bbc.rights.BusinessBotRights(),
		IsEnabled = !bbc.flags.HasFlag(BotBusinessConnection.Flags.disabled)
	};

	private async Task<InputPeer> GetBusinessPeer(string businessConnectionId)
	{
		await InitComplete();
		var updates = await Client.Account_GetBotBusinessConnection(businessConnectionId); //TODO cache
		updates.UserOrChat(_collector);
		var conn = updates.UpdateList.OfType<UpdateBotBusinessConnect>().First().connection;
		return updates.Users[conn.user_id];
	}

	internal Checklist Checklist(TodoList list, TodoCompletion[]? completions) => new()
	{
		Title = list.title.text,
		TitleEntities = MakeEntities(list.title.entities),
		OthersCanAddTasks = list.flags.HasFlag(TodoList.Flags.others_can_append),
		OthersCanMarkTasksAsDone = list.flags.HasFlag(TodoList.Flags.others_can_complete),
		Tasks = ChecklistTasks(list.list, completions)
	};

	private ChecklistTask[] ChecklistTasks(TodoItem[] items, TodoCompletion[]? completions = null) => [.. items.Select(item => {
		var completion = completions?.FirstOrDefault(compl => compl.id == item.id);
		return new ChecklistTask
		{
			Id = item.id,
			Text = item.title.text,
			TextEntities = MakeEntities(item.title.entities),
			CompletedByChat = completion?.completed_by is PeerChannel pc ? Chat(pc.channel_id) : null,
			CompletedByUser = completion?.completed_by is PeerUser pu ? User(pu.user_id) : null,
			CompletionDate = completion?.date
		};
	})];

	private TodoList MakeToDoList(InputChecklist list)
	{
		var text = ApplyParse(list.ParseMode, list.Title, list.TitleEntities, out var entities);
		return new TodoList
		{
			flags = (list.OthersCanAddTasks ? TodoList.Flags.others_can_append : 0)
				| (list.OthersCanMarkTasksAsDone ? TodoList.Flags.others_can_complete : 0),
			title = new TextWithEntities { text = text, entities = entities },
			list = [.. list.Tasks.Select(task => new TodoItem
			{
				id = task.Id,
				title = new TextWithEntities { text = ApplyParse(task.ParseMode, task.Text, task.TextEntities, out var iEntities), entities = iEntities }
			})],
		};
	}
}