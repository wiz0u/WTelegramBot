using System.Text;
using TL;

namespace WTelegram;

public partial class Bot
{
	/// <summary>Converts Client API TL.Update to Bot Telegram.Bot.Types.Update</summary>
	protected async Task<Update?> MakeUpdate(TL.Update update)
	{
		switch (update)
		{
			case UpdateNewMessage unm:
				if (unm.message is TL.Message msg && msg.flags.HasFlag(TL.Message.Flags.out_)) return null;
				bool isChannelPost = (await ChatFromPeer(unm.message.Peer))?.Type == ChatType.Channel;
				if (NotAllowed(isChannelPost ? UpdateType.ChannelPost : UpdateType.Message)) return null;
				var message = await MakeMessageAndReply(unm.message);
				if (message == null) return null;
				return isChannelPost ? new Update { ChannelPost = message, TLUpdate = update }
									: new Update { Message = message, TLUpdate = update };
			case UpdateEditMessage uem:
				if (uem.message is TL.Message emsg && emsg.flags.HasFlag(TL.Message.Flags.out_)) return null;
				isChannelPost = (await ChatFromPeer(uem.message.Peer))?.Type == ChatType.Channel;
				if (NotAllowed(isChannelPost ? UpdateType.ChannelPost : UpdateType.Message)) return null;
				return isChannelPost ? new Update { EditedChannelPost = await MakeMessageAndReply(uem.message), TLUpdate = update }
									: new Update { EditedMessage = await MakeMessageAndReply(uem.message), TLUpdate = update };
			case UpdateBotInlineQuery ubiq:
				if (NotAllowed(UpdateType.InlineQuery)) return null;
				return new Update
				{
					InlineQuery = new InlineQuery
					{
						Id = ubiq.query_id.ToString(),
						From = await UserOrResolve(ubiq.user_id),
						Query = ubiq.query,
						Offset = ubiq.offset,
						ChatType = ubiq.peer_type switch
						{
							InlineQueryPeerType.SameBotPM => ChatType.Sender,
							InlineQueryPeerType.PM or InlineQueryPeerType.BotPM => ChatType.Private,
							InlineQueryPeerType.Chat => ChatType.Group,
							InlineQueryPeerType.Megagroup => ChatType.Supergroup,
							InlineQueryPeerType.Broadcast => ChatType.Channel,
							_ => null,
						},
						Location = ubiq.geo.Location()
					},
					TLUpdate = update
				};
			case UpdateBotInlineSend ubis:
				if (NotAllowed(UpdateType.ChosenInlineResult)) return null;
				return new Update
				{
					ChosenInlineResult = new ChosenInlineResult
					{
						ResultId = ubis.id,
						From = await UserOrResolve(ubis.user_id),
						Location = ubis.geo.Location(),
						InlineMessageId = ubis.msg_id.InlineMessageId(),
						Query = ubis.query,
					},
					TLUpdate = update
				};
			case UpdateBotCallbackQuery ubcq:
				if (NotAllowed(UpdateType.CallbackQuery)) return null;
				return new Update
				{
					CallbackQuery = new CallbackQuery
					{
						Id = ubcq.query_id.ToString(),
						From = await UserOrResolve(ubcq.user_id),
						Message = await GetMIMessage(await ChatFromPeer(ubcq.peer, true), ubcq.msg_id, replyToo: true),
						ChatInstance = ubcq.chat_instance.ToString(),
						Data = ubcq.data == null ? null : Encoding.UTF8.GetString(ubcq.data),
						GameShortName = ubcq.game_short_name
					},
					TLUpdate = update
				};
			case UpdateInlineBotCallbackQuery ubicq:
				if (NotAllowed(UpdateType.CallbackQuery)) return null;
				return new Update
				{
					CallbackQuery = new CallbackQuery
					{
						Id = ubicq.query_id.ToString(),
						From = await UserOrResolve(ubicq.user_id),
						InlineMessageId = ubicq.msg_id.InlineMessageId(),
						ChatInstance = ubicq.chat_instance.ToString(),
						Data = ubicq.data == null ? null : Encoding.UTF8.GetString(ubicq.data),
						GameShortName = ubicq.game_short_name
					},
					TLUpdate = update
				};
			case UpdateChannelParticipant uchp:
				if (NotAllowed(uchp.actor_id == BotId ? UpdateType.MyChatMember : UpdateType.ChatMember)) return null;
				return MakeUpdate(new ChatMemberUpdated
				{
					Chat = await ChannelOrResolve(uchp.channel_id),
					From = await UserOrResolve(uchp.actor_id),
					Date = uchp.date,
					OldChatMember = uchp.prev_participant.ChatMember(await UserOrResolve((uchp.prev_participant ?? uchp.new_participant)!.UserId)),
					NewChatMember = uchp.new_participant.ChatMember(await UserOrResolve((uchp.new_participant ?? uchp.prev_participant)!.UserId)),
					InviteLink = await MakeChatInviteLink(uchp.invite),
					ViaJoinRequest = uchp.invite is ChatInvitePublicJoinRequests,
					ViaChatFolderInviteLink = uchp.flags.HasFlag(UpdateChannelParticipant.Flags.via_chatlist)
				}, update);
			case UpdateChatParticipant ucp:
				if (NotAllowed(ucp.actor_id == BotId ? UpdateType.MyChatMember : UpdateType.ChatMember)) return null;
				return MakeUpdate(new ChatMemberUpdated
				{
					Chat = await ChatOrResolve(ucp.chat_id),
					From = await UserOrResolve(ucp.actor_id),
					Date = ucp.date,
					OldChatMember = ucp.prev_participant.ChatMember(await UserOrResolve(ucp.prev_participant.UserId)),
					NewChatMember = ucp.new_participant.ChatMember(await UserOrResolve(ucp.new_participant.UserId)),
					InviteLink = await MakeChatInviteLink(ucp.invite)
				}, update);
			case UpdateBotStopped ubs:
				if (NotAllowed(ubs.user_id == BotId ? UpdateType.MyChatMember : UpdateType.ChatMember)) return null;
				var user = await UserOrResolve(ubs.user_id);
				var cmMember = new ChatMemberMember { User = user };
				var cmBanned = new ChatMemberBanned { User = user };
				return MakeUpdate(new ChatMemberUpdated
				{
					Chat = user.Chat(),
					From = user,
					Date = ubs.date,
					OldChatMember = ubs.stopped ? cmMember : cmBanned,
					NewChatMember = ubs.stopped ? cmBanned : cmMember
				}, update);
			case UpdateMessagePoll ump:
				if (NotAllowed(UpdateType.Poll)) return null;
				return new Update { Poll = MakePoll(ump.poll, ump.results), TLUpdate = update };
			case UpdateMessagePollVote umpv:
				if (NotAllowed(UpdateType.PollAnswer)) return null;
				return new Update
				{
					PollAnswer = new Telegram.Bot.Types.PollAnswer
					{
						PollId = umpv.poll_id.ToString(),
						VoterChat = umpv.peer is PeerChannel pc ? await ChannelOrResolve(pc.channel_id) : null,
						User = umpv.peer is PeerUser pu ? await UserOrResolve(pu.user_id) : null,
						OptionIds = umpv.options.Select(o => (int)o[0]).ToArray()
					},
					TLUpdate = update
				};
			case TL.UpdateBotChatInviteRequester ubcir:
				if (NotAllowed(UpdateType.ChatJoinRequest)) return null;
				return new Update
				{
					ChatJoinRequest = new Telegram.Bot.Types.ChatJoinRequest
					{
						Chat = (await ChatFromPeer(ubcir.peer))!,
						From = await UserOrResolve(ubcir.user_id),
						Date = ubcir.date,
						Bio = ubcir.about,
						UserChatId = ubcir.user_id,
						InviteLink = await MakeChatInviteLink(ubcir.invite)
					},
					TLUpdate = update
				};
			case TL.UpdateBotShippingQuery ubsq:
				if (NotAllowed(UpdateType.ShippingQuery)) return null;
				return new Update
				{
					ShippingQuery = new Telegram.Bot.Types.Payments.ShippingQuery
					{
						Id = ubsq.query_id.ToString(),
						From = await UserOrResolve(ubsq.user_id),
						InvoicePayload = Encoding.UTF8.GetString(ubsq.payload),
						ShippingAddress = ubsq.shipping_address.ShippingAddress()
					},
					TLUpdate = update
				};
			case TL.UpdateBotPrecheckoutQuery ubpq:
				if (NotAllowed(UpdateType.PreCheckoutQuery)) return null;
				return new Update
				{
					PreCheckoutQuery = new Telegram.Bot.Types.Payments.PreCheckoutQuery
					{
						Id = ubpq.query_id.ToString(),
						From = await UserOrResolve(ubpq.user_id),
						Currency = ubpq.currency,
						TotalAmount = (int)ubpq.total_amount,
						InvoicePayload = Encoding.UTF8.GetString(ubpq.payload),
						ShippingOptionId = ubpq.shipping_option_id,
						OrderInfo = ubpq.info.OrderInfo()
					},
					TLUpdate = update
				};
			case TL.UpdateBotBusinessConnect ubbc:
				if (NotAllowed(UpdateType.BusinessConnection)) return null;
				return new Update { BusinessConnection = await MakeBusinessConnection(ubbc.connection), TLUpdate = update };
			case TL.UpdateBotNewBusinessMessage ubnbm:
				if (NotAllowed(UpdateType.BusinessMessage)) return null;
				var replyToMessage = await MakeMessage(ubnbm.reply_to_message);
				if (replyToMessage != null) replyToMessage.BusinessConnectionId = ubnbm.connection_id;
				message = await MakeMessageAndReply(ubnbm.message, replyToMessage, ubnbm.connection_id);
				return message == null ? null : new Update { BusinessMessage = message, TLUpdate = update };
			case TL.UpdateBotEditBusinessMessage ubebm:
				if (NotAllowed(UpdateType.EditedBusinessMessage)) return null;
				replyToMessage = await MakeMessage(ubebm.reply_to_message);
				if (replyToMessage != null) replyToMessage.BusinessConnectionId = ubebm.connection_id;
				message = await MakeMessageAndReply(ubebm.message, replyToMessage, ubebm.connection_id);
				return message == null ? null : new Update { EditedBusinessMessage = message, TLUpdate = update };
			case TL.UpdateBotDeleteBusinessMessage ubdbm:
				if (NotAllowed(UpdateType.DeletedBusinessMessages)) return null;
				return new Update
				{
					DeletedBusinessMessages = new BusinessMessagesDeleted
					{
						BusinessConnectionId = ubdbm.connection_id,
						Chat = await ChatFromPeer(ubdbm.peer, true),
						MessageIds = ubdbm.messages
					},
					TLUpdate = update
				};
			case TL.UpdateBotMessageReaction ubmr:
				if (NotAllowed(UpdateType.MessageReaction)) return null;
				return new Update
				{
					MessageReaction = new MessageReactionUpdated
					{
						Chat = await ChatFromPeer(ubmr.peer, true),
						MessageId = ubmr.msg_id,
						User = await UserFromPeer(ubmr.actor),
						ActorChat = await ChatFromPeer(ubmr.actor),
						Date = ubmr.date,
						OldReaction = ubmr.old_reactions.Select(TypesTLConverters.ReactionType).ToArray(),
						NewReaction = ubmr.new_reactions.Select(TypesTLConverters.ReactionType).ToArray(),
					},
					TLUpdate = update
				};
			case TL.UpdateBotMessageReactions ubmrs:
				if (NotAllowed(UpdateType.MessageReactionCount)) return null;
				return new Update
				{
					MessageReactionCount = new MessageReactionCountUpdated
					{
						Chat = await ChatFromPeer(ubmrs.peer, true),
						MessageId = ubmrs.msg_id,
						Date = ubmrs.date,
						Reactions = ubmrs.reactions.Select(rc => new Telegram.Bot.Types.ReactionCount { Type = rc.reaction.ReactionType(), TotalCount = rc.count }).ToArray(),
					},
					TLUpdate = update
				};
			case TL.UpdateBotChatBoost ubcb:
				bool expired = ubcb.boost.expires < ubcb.boost.date;
				if (NotAllowed(expired ? UpdateType.RemovedChatBoost : UpdateType.ChatBoost)) return null;
				var cb = new ChatBoostUpdated
				{
					Chat = await ChatFromPeer(ubcb.peer, true),
					Boost = await MakeBoost(ubcb.boost)
				};
				return new Update
				{
					ChatBoost = expired ? null : cb,
					RemovedChatBoost = !expired ? null : new ChatBoostRemoved
					{
						Chat = cb.Chat,
						BoostId = cb.Boost.BoostId,
						RemoveDate = cb.Boost.AddDate,
						Source = cb.Boost.Source,
					},
					TLUpdate = update
				};
			//TL.UpdateDraftMessage seems used to update ourself user info
			default:
				return null;
		}
	}

	private Update? MakeUpdate(ChatMemberUpdated chatMember, TL.Update update) => chatMember.From?.Id == BotId
		? new Update { MyChatMember = chatMember, TLUpdate = update }
		: new Update { ChatMember = chatMember, TLUpdate = update };

	[return: NotNullIfNotNull(nameof(invite))]
	private async Task<ChatInviteLink?> MakeChatInviteLink(ExportedChatInvite? invite)
		=> invite switch
		{
			null => null,
			ChatInvitePublicJoinRequests => null,
			ChatInviteExported cie => new ChatInviteLink
			{
				InviteLink = cie.link,
				Creator = await UserOrResolve(cie.admin_id),
				CreatesJoinRequest = cie.flags.HasFlag(ChatInviteExported.Flags.request_needed),
				IsPrimary = cie.flags.HasFlag(ChatInviteExported.Flags.permanent),
				IsRevoked = cie.flags.HasFlag(ChatInviteExported.Flags.revoked),
				Name = cie.title,
				ExpireDate = cie.expire_date == default ? null : cie.expire_date,
				MemberLimit = cie.usage_limit == 0 ? null : cie.usage_limit,
				PendingJoinRequestCount = cie.flags.HasFlag(ChatInviteExported.Flags.has_requested) ? cie.requested : null,
			},
			_ => throw new WTException("Unexpected ExportedChatInvite: " + invite)
		};

	/// <returns>User or a stub on failure</returns>
	public async Task<User> UserOrResolve(long userId)
	{
		lock (_users)
			if (_users.TryGetValue(userId, out var user))
				return user;
		try
		{
			var users = await Client.Users_GetUsers(new InputUser(userId, 0));
			if (users.Length != 0 && users[0] is TL.User user)
				lock (_users)
					return _users[userId] = user.User();
		}
		catch (RpcException) { }
		return new User { Id = userId, FirstName = "" };
	}

	/// <returns>null if peer is not PeerUser ; User or a stub on failure</returns>
	private async Task<User?> UserFromPeer(Peer peer) => peer is not PeerUser pu ? null : await UserOrResolve(pu.user_id);

	private async Task<Chat> ChannelOrResolve(long id)
	{
		if (Chat(id) is { } chat)
			return chat;
		try
		{
			var chats = await Client.Channels_GetChannels(new InputChannel(id, 0));
			if (chats.chats.TryGetValue(id, out var chatBase))
				lock (_chats)
					return _chats[id] = chatBase.Chat();
		}
		catch (RpcException) { }
		return new Chat { Id = ZERO_CHANNEL_ID - id, Type = ChatType.Supergroup };
	}

	private async Task<Chat> ChatOrResolve(long chatId)
	{
		if (Chat(chatId) is { } chat)
			return chat;
		try
		{
			var chats = await Client.Messages_GetChats(chatId);
			if (chats.chats.TryGetValue(chatId, out var chatBase))
				lock (_chats)
					return _chats[chatId] = chatBase.Chat();
		}
		catch (RpcException) { }
		return new Chat { Id = -chatId, Type = ChatType.Group };
	}

	private async Task<Chat?> ChatFromPeer(Peer? peer, [DoesNotReturnIf(true)] bool allowUser = false) => peer switch
	{
		null => null,
		PeerUser pu => allowUser ? (await UserOrResolve(pu.user_id)).Chat() : null,
		PeerChannel pc => await ChannelOrResolve(pc.channel_id),
		_ => await ChatOrResolve(peer.ID),
	};

	private async Task<Chat> ChatFromPeer(InputPeer peer) => peer switch
	{
		InputPeerUser pu => (await UserOrResolve(pu.user_id)).Chat(),
		InputPeerChannel ipc => await ChannelOrResolve(ipc.channel_id),
		_ => await ChatOrResolve(peer.ID)
	};

	/// <summary>Handle UpdatesBase returned by various Client API and build the returned Bot Message</summary>
	protected async Task<Message> PostedMsg(Task<UpdatesBase> updatesTask, InputPeer peer, string? text = null, Message? replyToMessage = null)
	{
		var updates = await updatesTask;
		updates.UserOrChat(_collector);
		if (updates is UpdateShortSentMessage sent)
			return await FillTextAndMedia(new Message
			{
				MessageId = sent.id,
				From = await UserOrResolve(BotId),
				Date = sent.date,
				Chat = await ChatFromPeer(peer)!,
				ReplyToMessage = replyToMessage
			}, text, sent.entities, sent.media);
		foreach (var update in updates.UpdateList)
		{
			switch (update)
			{
				case UpdateNewMessage { message: { } message }: return (await MakeMessageAndReply(message, replyToMessage))!;
				case UpdateNewScheduledMessage { message: { } schedMsg }: return (await MakeMessageAndReply(schedMsg, replyToMessage))!;
				case UpdateEditMessage { message: { } editMsg }: return (await MakeMessageAndReply(editMsg, replyToMessage))!;
				case UpdateBotNewBusinessMessage { message: { } bizMsg } biz: return (await MakeMessageAndReply(bizMsg, replyToMessage, biz.connection_id))!;
			}
		}
		throw new WTException("Failed to retrieve sent message");
	}

	private async Task<Message[]> PostedMsgs(Task<UpdatesBase> updatesTask, int nbMsg, long startRandomId, Message? replyToMessage)
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
				case UpdateBotNewBusinessMessage { message: { } bizMsg } biz: msg = await MakeMessageAndReply(bizMsg, replyToMessage, biz.connection_id); break;
			}
			if (msg != null) result.Add(msg);
		}
		return [.. result.OrderBy(msg => msg.MessageId)];
	}

	/// <summary>Converts Client API TL.MessageBase to Bot Telegram.Bot.Types.Message and assign the ReplyToMessage/ExternalReply</summary>
	public async Task<Message?> MakeMessageAndReply(MessageBase? msgBase, Message? replyToMessage = null, string? bConnId = null)
	{
		var msg = await MakeMessage(msgBase);
		if (msg == null) return null;
		msg.BusinessConnectionId = bConnId;
		if (msgBase?.ReplyTo == null) return msg;
		if (msgBase.ReplyTo is MessageReplyHeader reply_to)
		{
			if (replyToMessage != null)
				msg.ReplyToMessage = replyToMessage;
			else if (reply_to.reply_to_msg_id > 0)
			{
				if (reply_to.reply_to_peer_id == null)
					msg.ReplyToMessage = await GetMessage(await ChatFromPeer(msgBase.Peer, true), reply_to.reply_to_msg_id);
				else
				{
					var ext = await FillTextAndMedia(new Message(), null, null!, reply_to.reply_media);
					msg.ExternalReply = new ExternalReplyInfo
					{
						MessageId = reply_to.reply_to_msg_id,
						Chat = await ChatFromPeer(reply_to.reply_to_peer_id),
						HasMediaSpoiler = ext.HasMediaSpoiler,
						LinkPreviewOptions = ext.LinkPreviewOptions,
						Origin = (await MakeOrigin(reply_to.reply_from))!,
						Animation = ext.Animation, Audio = ext.Audio, Contact = ext.Contact, Dice = ext.Dice, Document = ext.Document,
						Game = ext.Game, Giveaway = ext.Giveaway, GiveawayWinners = ext.GiveawayWinners, Invoice = ext.Invoice,
						Location = ext.Location, Photo = ext.Photo, Poll = ext.Poll, Sticker = ext.Sticker, Story = ext.Story,
						Venue = ext.Venue, Video = ext.Video, VideoNote = ext.VideoNote, Voice = ext.Voice, PaidMedia = ext.PaidMedia
					};
				}
			}
			if (reply_to.quote_text != null)
				msg.Quote = new TextQuote
				{
					Text = reply_to.quote_text,
					Entities = MakeEntities(reply_to.quote_entities),
					Position = reply_to.quote_offset,
					IsManual = reply_to.flags.HasFlag(MessageReplyHeader.Flags.quote)
				};
			if (msg.IsTopicMessage |= reply_to.flags.HasFlag(MessageReplyHeader.Flags.forum_topic))
				msg.MessageThreadId = reply_to.reply_to_top_id > 0 ? reply_to.reply_to_top_id : reply_to.reply_to_msg_id;
		}
		else if (msgBase.ReplyTo is MessageReplyStoryHeader mrsh)
			msg.ReplyToStory = new Story
			{
				Chat = await ChatFromPeer(mrsh.peer, true),
				Id = mrsh.story_id
			};
		return msg;
	}
	
	/// <summary>Converts Client API TL.MessageBase to Bot Telegram.Bot.Types.Message</summary>
	[return: NotNullIfNotNull(nameof(msgBase))]
	protected async Task<Message?> MakeMessage(MessageBase? msgBase)
	{
		switch (msgBase)
		{
			case TL.Message message:
				var msg = new WTelegram.Types.Message
				{
					TLMessage = message,
					MessageId = message.id,
					From = await UserFromPeer(message.from_id),
					SenderChat = await ChatFromPeer(message.from_id),
					Date = message.date,
					Chat = await ChatFromPeer(message.peer_id, allowUser: true),
					AuthorSignature = message.post_author,
					ReplyMarkup = message.reply_markup.InlineKeyboardMarkup(),
					SenderBoostCount = message.from_boosts_applied > 0 ? message.from_boosts_applied : null,
					SenderBusinessBot = User(message.via_business_bot_id),
					IsFromOffline = message.flags2.HasFlag(TL.Message.Flags2.offline),
					EffectId = message.flags2.HasFlag(TL.Message.Flags2.has_effect) ? message.effect.ToString() : null,
				};
				if (message.fwd_from is { } fwd)
				{
					msg.ForwardOrigin = await MakeOrigin(fwd);
					msg.IsAutomaticForward = msg.Chat.Type == ChatType.Supergroup && await ChatFromPeer(fwd.saved_from_peer) is Chat { Type: ChatType.Channel } && fwd.saved_from_msg_id != 0;
				}
				await FixMsgFrom(msg, message.from_id, message.peer_id);
				if (message.via_bot_id != 0) msg.ViaBot = await UserOrResolve(message.via_bot_id);
				if (message.edit_date != default) msg.EditDate = message.edit_date;
				if (message.flags.HasFlag(TL.Message.Flags.noforwards)) msg.HasProtectedContent = true;
				if (message.grouped_id != 0) msg.MediaGroupId = message.grouped_id.ToString();
				return await FillTextAndMedia(msg, message.message, message.entities, message.media, message.flags.HasFlag(TL.Message.Flags.invert_media));
			case TL.MessageService msgSvc:
				msg = new WTelegram.Types.Message
				{
					TLMessage = msgSvc,
					MessageId = msgSvc.id,
					From = await UserFromPeer(msgSvc.from_id),
					SenderChat = await ChatFromPeer(msgSvc.from_id),
					Date = msgSvc.date,
					Chat = await ChatFromPeer(msgSvc.peer_id, allowUser: true),
				};
				if (msgSvc.action is MessageActionTopicCreate)
				{
					msg.IsTopicMessage = true;
					msg.MessageThreadId = msgSvc.id;
				}
				await FixMsgFrom(msg, msgSvc.from_id, msgSvc.peer_id);
				if (await MakeServiceMessage(msgSvc, msg) == null) return null;
				return msg;
			case null:
				return null;
			default:
				return new WTelegram.Types.Message
				{
					TLMessage = msgBase,
					MessageId = msgBase.ID,
					Chat = await ChatFromPeer(msgBase.Peer, allowUser: true)!,
				};
		}

		async Task FixMsgFrom(Message msg, Peer from_id, Peer peer_id)
		{
			if (msg.From == null)
				switch (msg.Chat.Type)
				{
					case ChatType.Channel: break;
					case ChatType.Private:
						msg.From = await UserFromPeer(peer_id);
						break;
					default:
						if (from_id == null)
						{
							msg.From = GroupAnonymousBot;
							msg.SenderChat = msg.Chat;
						}
						else if (msg.IsAutomaticForward == true)
							msg.From = ServiceNotification;
						break;
				}
		}
	}

	private async Task<MessageOrigin?> MakeOrigin(MessageFwdHeader fwd)
	{
		MessageOrigin? origin = fwd.from_id switch
		{
			PeerUser pu => new MessageOriginUser { SenderUser = await UserOrResolve(pu.user_id) },
			PeerChat pc => new MessageOriginChat { SenderChat = await ChatOrResolve(pc.chat_id), AuthorSignature = fwd.post_author },
			PeerChannel pch => new MessageOriginChannel
			{
				Chat = await ChannelOrResolve(pch.channel_id),
				AuthorSignature = fwd.post_author,
				MessageId = fwd.channel_post
			},
			_ => fwd.from_name != null ? new MessageOriginHiddenUser { SenderUserName = fwd.from_name } : null
		};
		if (origin != null) origin.Date = fwd.date;
		return origin;
	}

	private async Task<Message> FillTextAndMedia(Message msg, string? text, TL.MessageEntity[] entities, MessageMedia media, bool invert_media = false)
	{
		switch (media)
		{
			case null:
				if (entities?.Any(e => e is MessageEntityUrl or MessageEntityTextUrl) == true)
					msg.LinkPreviewOptions = new LinkPreviewOptions { IsDisabled = true };
				msg.Text = text;
				msg.Entities = MakeEntities(entities);
				return msg;
			case MessageMediaWebPage mmwp:
				msg.LinkPreviewOptions = mmwp.LinkPreviewOptions(invert_media);
				msg.Text = text;
				msg.Entities = MakeEntities(entities);
				return msg;
			case MessageMediaDocument { document: TL.Document document } mmd:
				if (mmd.flags.HasFlag(MessageMediaDocument.Flags.spoiler)) msg.HasMediaSpoiler = true;
				msg.ShowCaptionAboveMedia = invert_media;
				var thumb = document.LargestThumbSize;
				if (mmd.flags.HasFlag(MessageMediaDocument.Flags.voice))
				{
					var audio = document.GetAttribute<DocumentAttributeAudio>();
					msg.Voice = new Telegram.Bot.Types.Voice
					{
						FileSize = document.size,
						Duration = (int)(audio?.duration + 0.5 ?? 0.0),
						MimeType = document.mime_type
					}.SetFileIds(document.ToFileLocation(), document.dc_id);
				}
				else if (mmd.flags.HasFlag(MessageMediaDocument.Flags.round))
				{
					var video = document.GetAttribute<DocumentAttributeVideo>();
					msg.VideoNote = new Telegram.Bot.Types.VideoNote
					{
						FileSize = document.size,
						Length = video?.w ?? 0,
						Duration = (int)(video?.duration + 0.5 ?? 0.0),
						 Thumbnail = thumb?.PhotoSize(document.ToFileLocation(thumb), document.dc_id)
					}.SetFileIds(document.ToFileLocation(), document.dc_id);
				}
				else if (mmd.flags.HasFlag(MessageMediaDocument.Flags.video))
					msg.Video = document.Video(thumb);
				else if (document.GetAttribute<DocumentAttributeAudio>() is { } audio)
				{
					msg.Audio = new Telegram.Bot.Types.Audio
					{
						FileSize = document.size,
						Duration = (int)(audio?.duration + 0.5 ?? 0.0),
						Performer = audio?.performer,
						Title = audio?.title,
						FileName = document.Filename,
						MimeType = document.mime_type,
						Thumbnail = thumb?.PhotoSize(document.ToFileLocation(thumb), document.dc_id)
					}.SetFileIds(document.ToFileLocation(), document.dc_id);
				}
				else if (document.GetAttribute<DocumentAttributeSticker>() is { } sticker)
				{
					msg.Sticker = await MakeSticker(document, sticker);
				}
				else
				{
					msg.Document = document.Document(thumb);
					if (document.GetAttribute<DocumentAttributeAnimated>() != null)
						msg.Animation = MakeAnimation(msg.Document!, document.GetAttribute<DocumentAttributeVideo>());
				}
				break;
			case MessageMediaPhoto { photo: TL.Photo photo } mmp:
				if (mmp.flags.HasFlag(MessageMediaPhoto.Flags.spoiler)) msg.HasMediaSpoiler = true;
				msg.ShowCaptionAboveMedia = invert_media;
				msg.Photo = photo.PhotoSizes();
				break;
			case MessageMediaVenue mmv:
				msg.Venue = new Venue
				{
					Location = mmv.geo.Location(),
					Title = mmv.title,
					Address = mmv.address,
					FoursquareId = mmv.provider == "foursquare" ? mmv.venue_id : null,
					FoursquareType = mmv.provider == "foursquare" ? mmv.venue_type : null,
					GooglePlaceId = mmv.provider == "gplaces" ? mmv.venue_id : null,
					GooglePlaceType = mmv.provider == "gplaces" ? mmv.venue_id : null
				};
				break;
			case MessageMediaContact mmc:
				msg.Contact = new Telegram.Bot.Types.Contact
				{
					PhoneNumber = mmc.phone_number,
					FirstName = mmc.first_name,
					LastName = mmc.last_name,
					UserId = mmc.user_id,
					Vcard = mmc.vcard,
				};
				break;
			case MessageMediaGeo mmg:
				msg.Location = mmg.geo.Location();
				break;
			case MessageMediaGeoLive mmgl:
				msg.Location = mmgl.geo.Location();
				msg.Location.LivePeriod = mmgl.period;
				msg.Location.Heading = mmgl.flags.HasFlag(MessageMediaGeoLive.Flags.has_heading) ? mmgl.heading : null;
				msg.Location.ProximityAlertRadius = mmgl.flags.HasFlag(MessageMediaGeoLive.Flags.has_proximity_notification_radius) ? mmgl.proximity_notification_radius : null;
				break;
			case MessageMediaPoll { poll: TL.Poll poll, results: TL.PollResults pollResults }:
				msg.Poll = MakePoll(poll, pollResults);
				return msg;
			case MessageMediaDice mmd:
				msg.Dice = new Dice { Emoji = mmd.emoticon, Value = mmd.value };
				return msg;
			case MessageMediaInvoice mmi:
				msg.Invoice = new Telegram.Bot.Types.Payments.Invoice
				{
					Title = mmi.title,
					Description = mmi.description,
					StartParameter = mmi.start_param,
					Currency = mmi.currency,
					TotalAmount = (int)mmi.total_amount
				};
				return msg;
			case MessageMediaGame mmg:
				msg.Game = new Telegram.Bot.Types.Game
				{
					Title = mmg.game.title,
					Description = mmg.game.description,
					Photo = mmg.game.photo.PhotoSizes()!,
					Text = text == "" ? null : text,
					TextEntities = MakeEntities(entities)
				};
				if (mmg.game.document is TL.Document doc && doc.GetAttribute<DocumentAttributeAnimated>() != null)
				{
					thumb = doc.LargestThumbSize;
					msg.Game.Animation = MakeAnimation(doc.Document(thumb)!, doc.GetAttribute<DocumentAttributeVideo>());
				}
				return msg;
			case MessageMediaStory mms:
				msg.Story = new Story
				{
					Chat = await ChatFromPeer(mms.peer, true),
					Id = mms.id
				};
				break;
			case MessageMediaGiveaway mmg:
				msg.Giveaway = new Giveaway
				{
					Chats = await mmg.channels.Select(ChannelOrResolve).WhenAllSequential(),
					WinnersSelectionDate = mmg.until_date,
					WinnerCount = mmg.quantity,
					OnlyNewMembers = mmg.flags.HasFlag(MessageMediaGiveaway.Flags.only_new_subscribers),
					HasPublicWinners = mmg.flags.HasFlag(MessageMediaGiveaway.Flags.winners_are_visible),
					PrizeDescription = mmg.prize_description,
					CountryCodes = mmg.countries_iso2,
					PremiumSubscriptionMonthCount = mmg.months
				};
				break;
			case MessageMediaGiveawayResults mmgr:
				msg.GiveawayWinners = new GiveawayWinners
				{
					Chat = await ChannelOrResolve(mmgr.channel_id),
					GiveawayMessageId = mmgr.launch_msg_id,
					WinnersSelectionDate = mmgr.until_date,
					WinnerCount = mmgr.winners_count,
					Winners = await mmgr.winners.Select(UserOrResolve).WhenAllSequential(),
					AdditionalChatCount = mmgr.additional_peers_count,
					PremiumSubscriptionMonthCount = mmgr.months,
					UnclaimedPrizeCount = mmgr.unclaimed_count,
					OnlyNewMembers = mmgr.flags.HasFlag(MessageMediaGiveawayResults.Flags.only_new_subscribers),
					WasRefunded = mmgr.flags.HasFlag(MessageMediaGiveawayResults.Flags.refunded),
					PrizeDescription = mmgr.prize_description,
				};
				break;
			case MessageMediaPaidMedia mmpm:
				msg.PaidMedia = new PaidMediaInfo
				{
					StarCount = (int)mmpm.stars_amount,
					PaidMedia = mmpm.extended_media.Select(TypesTLConverters.PaidMedia).ToArray()
				};
				break;
			default:
				break;
		}
		if (text != "") msg.Caption = text;
		msg.CaptionEntities = MakeEntities(entities);
		return msg;
	}

	private async Task<object?> MakeServiceMessage(MessageService msgSvc, Message msg)
	{
		return msgSvc.action switch
		{
			MessageActionChatAddUser macau => msg.NewChatMembers = await macau.users.Select(UserOrResolve).WhenAllSequential(),
			MessageActionChatDeleteUser macdu => msg.LeftChatMember = await UserOrResolve(macdu.user_id),
			MessageActionChatEditTitle macet => msg.NewChatTitle = macet.title,
			MessageActionChatEditPhoto macep => msg.NewChatPhoto = macep.photo.PhotoSizes(),
			MessageActionChatDeletePhoto macdp => msg.DeleteChatPhoto = true,
			MessageActionChatCreate => msg.GroupChatCreated = true,
			MessageActionChannelCreate => (await ChatFromPeer(msgSvc.peer_id))?.Type == ChatType.Channel
				? msg.SupergroupChatCreated = true : msg.ChannelChatCreated = true,
			MessageActionSetMessagesTTL macsmt => msg.MessageAutoDeleteTimerChanged =
				new MessageAutoDeleteTimerChanged { MessageAutoDeleteTime = macsmt.period },
			MessageActionChatMigrateTo macmt => msg.MigrateToChatId = ZERO_CHANNEL_ID - macmt.channel_id,
			MessageActionChannelMigrateFrom macmf => msg.MigrateFromChatId = -macmf.chat_id,
			MessageActionPinMessage macpm => msg.PinnedMessage = await GetMIMessage(
				await ChatFromPeer(msgSvc.peer_id, allowUser: true), msgSvc.reply_to is MessageReplyHeader mrh ? mrh.reply_to_msg_id : 0),
			MessageActionChatJoinedByLink or MessageActionChatJoinedByRequest => msg.NewChatMembers = [msg.From!],
			MessageActionPaymentSentMe mapsm => msg.SuccessfulPayment = new Telegram.Bot.Types.Payments.SuccessfulPayment
			{
				Currency = mapsm.currency,
				TotalAmount = (int)mapsm.total_amount,
				InvoicePayload = Encoding.UTF8.GetString(mapsm.payload),
				ShippingOptionId = mapsm.shipping_option_id,
				OrderInfo = mapsm.info.OrderInfo(),
				TelegramPaymentChargeId = mapsm.charge.id,
				ProviderPaymentChargeId = mapsm.charge.provider_charge_id
			},
			MessageActionRequestedPeer { peers.Length: > 0 } marp => marp.peers[0] is PeerUser
				? msg.UsersShared = new UsersShared { RequestId = marp.button_id, Users = marp.peers.Select(p => new SharedUser { UserId = p.ID }).ToArray() }
				: msg.ChatShared = new ChatShared { RequestId = marp.button_id, ChatId = marp.peers[0].ID },
			MessageActionRequestedPeerSentMe { peers.Length: > 0 } marpsm => marpsm.peers[0] is RequestedPeerUser
				? msg.UsersShared = new UsersShared { RequestId = marpsm.button_id, Users = marpsm.peers.Select(p => p.ToSharedUser()).ToArray() }
				: msg.ChatShared = marpsm.peers[0].ToSharedChat(marpsm.button_id),
			MessageActionBotAllowed maba => maba switch
			{
				{ domain: not null } => msg.ConnectedWebsite = maba.domain,
				{ app: not null } => msg.WriteAccessAllowed = new WriteAccessAllowed {
					WebAppName = maba.app.short_name,
					FromRequest = maba.flags.HasFlag(MessageActionBotAllowed.Flags.from_request),
					FromAttachmentMenu = maba.flags.HasFlag(MessageActionBotAllowed.Flags.attach_menu) },
				_ => null
			},
			MessageActionSecureValuesSentMe masvsm => msg.PassportData = masvsm.PassportData(),
			MessageActionGeoProximityReached magpr => msg.ProximityAlertTriggered = new ProximityAlertTriggered
			{
				Traveler = (await UserFromPeer(magpr.from_id))!,
				Watcher = (await UserFromPeer(magpr.to_id))!,
				Distance = magpr.distance
			},
			MessageActionGroupCallScheduled magcs => msg.VideoChatScheduled = new VideoChatScheduled { StartDate = magcs.schedule_date },
			MessageActionGroupCall magc => magc.flags.HasFlag(MessageActionGroupCall.Flags.has_duration)
				? msg.VideoChatEnded = new VideoChatEnded { Duration = magc.duration }
				: msg.VideoChatStarted = new VideoChatStarted(),
			MessageActionInviteToGroupCall maitgc => msg.VideoChatParticipantsInvited = new VideoChatParticipantsInvited {
				Users = await maitgc.users.Select(UserOrResolve).WhenAllSequential() },
			MessageActionWebViewDataSentMe mawvdsm => msg.WebAppData = new WebAppData { ButtonText = mawvdsm.text, Data = mawvdsm.data },
			MessageActionTopicCreate matc => msg.ForumTopicCreated = new ForumTopicCreated { Name = matc.title, IconColor = matc.icon_color,
				IconCustomEmojiId = matc.flags.HasFlag(MessageActionTopicCreate.Flags.has_icon_emoji_id) ? matc.icon_emoji_id.ToString() : null },
			MessageActionTopicEdit mate => mate.flags.HasFlag(MessageActionTopicEdit.Flags.has_closed) ?
					mate.closed ? msg.ForumTopicClosed = new() : msg.ForumTopicReopened = new()
				: mate.flags.HasFlag(MessageActionTopicEdit.Flags.has_hidden)
					? mate.hidden ? msg.GeneralForumTopicHidden = new() : msg.GeneralForumTopicUnhidden = new()
					: msg.ForumTopicEdited = new ForumTopicEdited { Name = mate.title, IconCustomEmojiId = mate.icon_emoji_id != 0
						? mate.icon_emoji_id.ToString() : mate.flags.HasFlag(MessageActionTopicEdit.Flags.has_icon_emoji_id) ? "" : null },
			MessageActionBoostApply maba => msg.BoostAdded = new ChatBoostAdded { BoostCount = maba.boosts },
			MessageActionGiveawayLaunch magl => msg.GiveawayCreated = new GiveawayCreated(),
			MessageActionGiveawayResults magr => msg.GiveawayCompleted = new GiveawayCompleted {
				WinnerCount = magr.winners_count, UnclaimedPrizeCount = magr.unclaimed_count,
				GiveawayMessage = msgSvc.reply_to is MessageReplyHeader mrh ? await GetMessage(await ChatFromPeer(msgSvc.peer_id, true), mrh.reply_to_msg_id) : null,
			},
			MessageActionSetChatWallPaper mascwp => msg.ChatBackgroundSet = new ChatBackground { Type = mascwp.wallpaper.BackgroundType() },
			MessageActionPaymentRefunded mapr => msg.RefundedPayment = new RefundedPayment { 
				Currency = mapr.currency, TotalAmount = (int)mapr.total_amount,
				InvoicePayload = mapr.payload == null ? "" : Encoding.UTF8.GetString(mapr.payload),
				TelegramPaymentChargeId = mapr.charge.id, ProviderPaymentChargeId = mapr.charge.provider_charge_id
			},
			_ => null,
		};
	}

	private static Animation MakeAnimation(Telegram.Bot.Types.Document msgDoc, DocumentAttributeVideo video) => new()
	{
		FileSize = msgDoc.FileSize,
		Width = video?.w ?? 0,
		Height = video?.h ?? 0,
		Duration = (int)(video?.duration + 0.5 ?? 0.0),
		Thumbnail = msgDoc.Thumbnail,
		FileName = msgDoc.FileName,
		MimeType = msgDoc.MimeType,
		FileId = msgDoc.FileId,
		FileUniqueId = msgDoc.FileUniqueId
	};

	private Telegram.Bot.Types.Poll MakePoll(TL.Poll poll, PollResults pollResults)
	{
		int? correctOption = pollResults.results == null ? null : Array.FindIndex(pollResults.results, pav => pav.flags.HasFlag(PollAnswerVoters.Flags.correct));
		return new Telegram.Bot.Types.Poll
		{
			Id = poll.id.ToString(),
			Question = poll.question.text,
			Options = poll.answers.Select((pa, i) => new PollOption { Text = pa.text.text, VoterCount = pollResults.results?[i].voters ?? 0 }).ToArray(),
			TotalVoterCount = pollResults.total_voters,
			IsClosed = poll.flags.HasFlag(TL.Poll.Flags.closed),
			IsAnonymous = !poll.flags.HasFlag(TL.Poll.Flags.public_voters),
			Type = poll.flags.HasFlag(TL.Poll.Flags.quiz) ? "quiz" : "regular",
			AllowsMultipleAnswers = poll.flags.HasFlag(TL.Poll.Flags.multiple_choice),
			CorrectOptionId = correctOption < 0 ? null : correctOption,
			Explanation = pollResults.solution,
			ExplanationEntities = MakeEntities(pollResults.solution_entities),
			OpenPeriod = poll.close_period == default ? null : poll.close_period,
			CloseDate = poll.close_date == default ? null : poll.close_date
		};
	}

	private TL.PollAnswer MakePollAnswer(InputPollOption ipo, int index)
	{
		var text = ipo.Text;
		var entities = ApplyParse(ipo.TextParseMode, ref text, ipo.TextEntities);
		return new()
		{
			text = new() { text = text, entities = entities },
			option = [(byte)index]
		};
	}
}
