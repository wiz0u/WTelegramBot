using System.Text;
using TL;
using Telegram.Bot.Types.Enums;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Telegram.Bot;

public partial class TelegramBotClient
{
	/// <summary>Converts Client API TL.Update to Bot Types.Update</summary>
	protected async Task<Update?> MakeUpdate(TL.Update update)
	{
		switch (update)
		{
			case UpdateNewMessage unm:
				if (unm.message is TL.Message msg && msg.flags.HasFlag(TL.Message.Flags.out_)) return null;
				bool isChannelPost = (await ChatFromPeer(unm.message.Peer))?.Type == ChatType.Channel;
				if (NotAllowed(isChannelPost ? UpdateType.ChannelPost : UpdateType.Message)) return null;
				var replyToMessage = await GetReplyToMessage(unm.message.ReplyTo, unm.message.Peer);
				var message = await MakeMessage(unm.message, replyToMessage);
				if (message == null) return null;
				return isChannelPost ? new Update { ChannelPost = message, RawUpdate = update }
										: new Update { Message = message, RawUpdate = update };
			case UpdateEditMessage uem:
				if (uem.message is TL.Message emsg && emsg.flags.HasFlag(TL.Message.Flags.out_)) return null;
				isChannelPost = (await ChatFromPeer(uem.message.Peer))?.Type == ChatType.Channel;
				if (NotAllowed(isChannelPost ? UpdateType.ChannelPost : UpdateType.Message)) return null;
				replyToMessage = await GetReplyToMessage(uem.message.ReplyTo, uem.message.Peer);
				return isChannelPost ? new Update { EditedChannelPost = await MakeMessage(uem.message, replyToMessage), RawUpdate = update }
										: new Update { EditedMessage = await MakeMessage(uem.message, replyToMessage), RawUpdate = update };
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
					RawUpdate = update
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
					RawUpdate = update
				};
			case UpdateBotCallbackQuery ubcq:
				if (NotAllowed(UpdateType.CallbackQuery)) return null;
				return new Update
				{
					CallbackQuery = new CallbackQuery
					{
						Id = ubcq.query_id.ToString(),
						From = await UserOrResolve(ubcq.user_id),
						Message = await GetMessage(await ChatFromPeer(ubcq.peer, true), ubcq.msg_id),
						ChatInstance = ubcq.chat_instance.ToString(),
						Data = ubcq.data == null ? null : Encoding.UTF8.GetString(ubcq.data),
						GameShortName = ubcq.game_short_name
					},
					RawUpdate = update
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
					RawUpdate = update
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
				return new Update { Poll = MakePoll(ump.poll, ump.results), RawUpdate = update };
			case UpdateMessagePollVote umpv:
				if (NotAllowed(UpdateType.PollAnswer)) return null;
				user = umpv.peer is PeerUser pu ? await UserOrResolve(pu.user_id) : null;
				return new Update
				{
					PollAnswer = new Types.PollAnswer
					{
						PollId = umpv.poll_id.ToString(),
						User = user!,
						OptionIds = umpv.options.Select(o => (int)o[0]).ToArray()
					},
					RawUpdate = update
				};
			case TL.UpdateBotChatInviteRequester ubcir:
				if (NotAllowed(UpdateType.ChatJoinRequest)) return null;
				return new Update
				{
					ChatJoinRequest = new Types.ChatJoinRequest
					{
						Chat = (await ChatFromPeer(ubcir.peer))!,
						From = await UserOrResolve(ubcir.user_id),
						Date = ubcir.date,
						Bio = ubcir.about,
						UserChatId = ubcir.user_id,
						InviteLink = await MakeChatInviteLink(ubcir.invite)
					},
					RawUpdate = update
				};
			case TL.UpdateBotShippingQuery ubsq:
				if (NotAllowed(UpdateType.ShippingQuery)) return null;
				return new Update
				{
					ShippingQuery = new Types.Payments.ShippingQuery
					{
						Id = ubsq.query_id.ToString(),
						From = await UserOrResolve(ubsq.user_id),
						InvoicePayload = Encoding.UTF8.GetString(ubsq.payload),
						ShippingAddress = ubsq.shipping_address.ShippingAddress()
					},
					RawUpdate = update
				};
			case TL.UpdateBotPrecheckoutQuery ubpq:
				if (NotAllowed(UpdateType.PreCheckoutQuery)) return null;
				return new Update
				{
					PreCheckoutQuery = new Types.Payments.PreCheckoutQuery
					{
						Id = ubpq.query_id.ToString(),
						From = await UserOrResolve(ubpq.user_id),
						Currency = ubpq.currency,
						TotalAmount = (int)ubpq.total_amount,
						InvoicePayload = Encoding.UTF8.GetString(ubpq.payload),
						ShippingOptionId = ubpq.shipping_option_id,
						OrderInfo = ubpq.info.OrderInfo()
					},
					RawUpdate = update
				};
			//TL.UpdateDraftMessage seems used to update ourself user info
			default:
				return null;
		}
	}

	private Update? MakeUpdate(ChatMemberUpdated chatMember, TL.Update update) => chatMember.From?.Id == BotId
		? new Update { MyChatMember = chatMember, RawUpdate = update }
		: new Update { ChatMember = chatMember, RawUpdate = update };

	[return: NotNullIfNotNull(nameof(invite))]
	private async Task<ChatInviteLink?> MakeChatInviteLink(ExportedChatInvite? invite)
		=> invite switch
		{
			null => null,
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
			_ => throw new ApiRequestException("Unexpected ExportedChatInvite: " + invite)
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

	private async Task<Message?> GetReplyToMessage(MessageReplyHeaderBase replyTo, Peer peer)
	{
		if (replyTo is not MessageReplyHeader reply_to) return null;
		if (reply_to.reply_to_msg_id == 0) return null;
		if (reply_to.reply_to_peer_id != null) peer = reply_to.reply_to_peer_id;
		return await GetReplyToMessage(await ChatFromPeer(peer, true), reply_to.reply_to_msg_id, true);
	}

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
				case UpdateNewMessage { message: { } message }: return (await MakeMessage(message, replyToMessage))!;
				case UpdateNewScheduledMessage { message: { } schedMsg }: return (await MakeMessage(schedMsg, replyToMessage))!;
				case UpdateEditMessage { message: { } editMsg }: return (await MakeMessage(editMsg, replyToMessage))!;
			}
		}
		throw new ApiRequestException("Failed to retrieve sent message");
	}

	private async Task<Message[]> PostedMsgs(Task<UpdatesBase> updatesTask, int nbMsg, long startRandomId, Message? replyToMessage)
	{
		var updates = await updatesTask;
		updates.UserOrChat(_collector);
		int[] msgIds = new int[nbMsg];
		var result = new Message[nbMsg];
		foreach (var update in updates.UpdateList)
		{
			switch (update)
			{
				case UpdateMessageID updMsgId: msgIds[(int)(updMsgId.random_id - startRandomId)] = updMsgId.id; break;
				case UpdateNewMessage { message: TL.Message message }: result[Array.IndexOf(msgIds, message.id)] = (await MakeMessage(message, replyToMessage))!; break;
				case UpdateNewScheduledMessage { message: TL.Message schedMsg }: result[Array.IndexOf(msgIds, schedMsg.id)] = (await MakeMessage(schedMsg, replyToMessage))!; break;
			}
		}
		return result;
	}

	/// <summary>Converts Client API TL.MessageBase to Bot Types.Message</summary>
	[return: NotNullIfNotNull(nameof(msgBase))]
	protected async Task<Message?> MakeMessage(MessageBase? msgBase, Message? replyToMessage = null)
	{
		switch (msgBase)
		{
			case TL.Message message:
				var reply_to = message.reply_to as MessageReplyHeader;
				var msg = new Message
				{
					MessageId = message.id,
					From = await UserFromPeer(message.from_id),
					SenderChat = await ChatFromPeer(message.from_id),
					Date = message.date,
					Chat = await ChatFromPeer(message.peer_id, allowUser: true),
					ReplyToMessage = replyToMessage,
					AuthorSignature = message.post_author,
					ReplyMarkup = message.reply_markup.InlineKeyboardMarkup(),
					IsTopicMessage = reply_to?.flags.HasFlag(MessageReplyHeader.Flags.forum_topic),
				};
				if (msg.IsTopicMessage == true)
					msg.MessageThreadId = reply_to.reply_to_top_id > 0 ? reply_to.reply_to_top_id : reply_to.reply_to_msg_id;
				if (message.fwd_from is MessageFwdHeader fwd)
				{
					msg.ForwardFrom = await UserFromPeer(fwd.from_id);
					msg.ForwardFromChat = await ChatFromPeer(fwd.from_id);
					msg.ForwardFromMessageId = fwd.flags.HasFlag(MessageFwdHeader.Flags.has_channel_post) ? fwd.channel_post : null;
					msg.ForwardSignature = fwd.post_author;
					msg.ForwardSenderName = fwd.from_name;
					msg.ForwardDate = fwd.date;
					msg.IsAutomaticForward = msg.Chat.Type == ChatType.Supergroup && await ChatFromPeer(fwd.saved_from_peer) is Chat { Type: ChatType.Channel } && fwd.saved_from_msg_id != 0;
				}
				await FixMsgFrom(msg, message.from_id, message.peer_id);
				if (message.via_bot_id != 0) msg.ViaBot = await UserOrResolve(message.via_bot_id);
				if (message.edit_date != default) msg.EditDate = message.edit_date;
				if (message.flags.HasFlag(TL.Message.Flags.noforwards)) msg.HasProtectedContent = true;
				if (message.grouped_id != 0) msg.MediaGroupId = message.grouped_id.ToString();
				return await FillTextAndMedia(msg, message.message, message.entities, message.media);
			case TL.MessageService msgSvc:
				reply_to = msgSvc.reply_to as MessageReplyHeader;
				msg = new Message
				{
					MessageId = msgSvc.id,
					From = await UserFromPeer(msgSvc.from_id),
					SenderChat = await ChatFromPeer(msgSvc.from_id),
					Date = msgSvc.date,
					Chat = await ChatFromPeer(msgSvc.peer_id, allowUser: true),
					ReplyToMessage = replyToMessage,
					IsTopicMessage = reply_to?.flags.HasFlag(MessageReplyHeader.Flags.forum_topic),
				};
				if (msg.IsTopicMessage == true)
					msg.MessageThreadId = reply_to.reply_to_top_id > 0 ? reply_to.reply_to_top_id : reply_to.reply_to_msg_id;
				else if (reply_to == null && msgSvc.action is MessageActionTopicCreate)
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
				return new Message
				{
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

	private async Task<Message> FillTextAndMedia(Message msg, string? text, MessageEntity[] entities, MessageMedia media)
	{
		switch (media)
		{
			case MessageMediaWebPage:
			case null:
				msg.Text = text;
				msg.Entities = entities;
				return msg;
			case MessageMediaDocument { document: TL.Document document } mmd:
				if (mmd.flags.HasFlag(MessageMediaDocument.Flags.spoiler)) msg.HasMediaSpoiler = true;
				var thumb = document.LargestThumbSize;
				if (mmd.flags.HasFlag(MessageMediaDocument.Flags.voice))
				{
					var audio = document.GetAttribute<DocumentAttributeAudio>();
					msg.Voice = new Types.Voice
					{
						FileSize = document.size,
						Duration = (int)(audio?.duration + 0.5 ?? 0.0),
						MimeType = document.mime_type
					}.SetFileIds(document.ToFileLocation(), document.dc_id);
				}
				else if (mmd.flags.HasFlag(MessageMediaDocument.Flags.round))
				{
					var video = document.GetAttribute<DocumentAttributeVideo>();
					msg.VideoNote = new Types.VideoNote
					{
						FileSize = document.size,
						Length = video?.w ?? 0,
						Duration = (int)(video?.duration + 0.5 ?? 0.0),
						 Thumbnail = thumb?.PhotoSize(document.ToFileLocation(thumb), document.dc_id)
					}.SetFileIds(document.ToFileLocation(), document.dc_id);
				}
				else if (mmd.flags.HasFlag(MessageMediaDocument.Flags.video))
				{
					var video = document.GetAttribute<DocumentAttributeVideo>();
					msg.Video = new Types.Video
					{
						FileSize = document.size,
						Width = video?.w ?? 0,
						Height = video?.h ?? 0,
						Duration = (int)(video?.duration + 0.5 ?? 0.0),
						Thumbnail = thumb?.PhotoSize(document.ToFileLocation(thumb), document.dc_id),
						FileName = document.Filename,
						MimeType = document.mime_type
					}.SetFileIds(document.ToFileLocation(), document.dc_id);
				}
				else if (document.GetAttribute<DocumentAttributeAudio>() is { } audio)
				{
					msg.Audio = new Types.Audio
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
					msg.Document = new Types.Document
					{
						FileSize = document.size,
						Thumbnail = thumb?.PhotoSize(document.ToFileLocation(thumb), document.dc_id),
						FileName = document.Filename,
						MimeType = document.mime_type
					}.SetFileIds(document.ToFileLocation(), document.dc_id);
					if (document.GetAttribute<DocumentAttributeAnimated>() != null)
						msg.Animation = MakeAnimation(msg.Document, document.GetAttribute<DocumentAttributeVideo>());
				}
				break;
			case MessageMediaPhoto { photo: TL.Photo photo } mmp:
				if (mmp.flags.HasFlag(MessageMediaPhoto.Flags.spoiler)) msg.HasMediaSpoiler = true;
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
				msg.Contact = new Types.Contact
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
				msg.Invoice = new Types.Payments.Invoice
				{
					Title = mmi.title,
					Description = mmi.description,
					StartParameter = mmi.start_param,
					Currency = mmi.currency,
					TotalAmount = (int)mmi.total_amount
				};
				return msg;
			case MessageMediaGame mmg:
				msg.Game = new Types.Game
				{
					Title = mmg.game.title,
					Description = mmg.game.description,
					Photo = mmg.game.photo.PhotoSizes()!,
					Text = text == "" ? null : text,
					TextEntities = entities
				};
				if (mmg.game.document is TL.Document doc && doc.GetAttribute<DocumentAttributeAnimated>() != null)
				{
					thumb = doc.LargestThumbSize;
					var msgDoc = new Types.Document
					{
						FileSize = doc.size,
						Thumbnail = thumb?.PhotoSize(doc.ToFileLocation(thumb), doc.dc_id),
						FileName = doc.Filename,
						MimeType = doc.mime_type
					}.SetFileIds(doc.ToFileLocation(), doc.dc_id);
					msg.Game.Animation = MakeAnimation(msgDoc, doc.GetAttribute<DocumentAttributeVideo>());
				}
				return msg;
			default:
				System.Diagnostics.Debugger.Break();
				break;
		}
		if (text != "") msg.Caption = text;
		msg.CaptionEntities = entities;
		return msg;
	}

	private async Task<object?> MakeServiceMessage(MessageService msgSvc, Message msg)
	{
		return msgSvc.action switch
		{
			MessageActionChatAddUser macau => msg.NewChatMembers = await macau.users.Select(id => UserOrResolve(id)).WhenAllSequential(),
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
			MessageActionPinMessage macpm => msg.PinnedMessage = await GetMessage(
				await ChatFromPeer(msgSvc.peer_id, allowUser: true), msgSvc.reply_to is MessageReplyHeader mrh ? mrh.reply_to_msg_id : 0),
			MessageActionChatJoinedByLink or MessageActionChatJoinedByRequest => msg.NewChatMembers = [msg.From!],
			MessageActionPaymentSentMe mapsm => msg.SuccessfulPayment = new Types.Payments.SuccessfulPayment
			{
				Currency = mapsm.currency,
				TotalAmount = (int)mapsm.total_amount,
				InvoicePayload = Encoding.UTF8.GetString(mapsm.payload),
				ShippingOptionId = mapsm.shipping_option_id,
				OrderInfo = mapsm.info.OrderInfo(),
				TelegramPaymentChargeId = mapsm.charge.id,
				ProviderPaymentChargeId = mapsm.charge.provider_charge_id
			},
			MessageActionRequestedPeer marp when marp.peers?.Length > 0 => marp.peers[0] is PeerUser pu
				? msg.UserShared = new UserShared { RequestId = marp.button_id, UserId = pu.user_id }
				: msg.ChatShared = new ChatShared { RequestId = marp.button_id, ChatId = marp.peers[0].ID },
			MessageActionRequestedPeerSentMe marpsm when marpsm.peers?.Length > 0 => marpsm.peers[0] is RequestedPeerUser rpu
				? msg.UserShared = new UserShared { RequestId = marpsm.button_id, UserId = rpu.user_id }
				: msg.ChatShared = new ChatShared { RequestId = marpsm.button_id, ChatId = marpsm.peers[0].ID },
			MessageActionBotAllowed maba => maba switch
			{
				{ domain: not null } => msg.ConnectedWebsite = maba.domain,
				{ app: not null } => msg.WriteAccessAllowed = new WriteAccessAllowed { WebAppName = maba.app.short_name },
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
			_ => null,
		};
	}

	private static Animation MakeAnimation(Types.Document msgDoc, DocumentAttributeVideo video) => new()
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

	private static Types.Poll MakePoll(TL.Poll poll, PollResults pollResults)
	{
		int correctOption = Array.FindIndex(pollResults.results, pav => pav.flags.HasFlag(PollAnswerVoters.Flags.correct));
		return new Types.Poll
		{
			Id = poll.id.ToString(),
			Question = poll.question,
			Options = poll.answers.Select((pa, i) => new PollOption { Text = pa.text, VoterCount = pollResults.results[i].voters }).ToArray(),
			TotalVoterCount = pollResults.total_voters,
			IsClosed = poll.flags.HasFlag(TL.Poll.Flags.closed),
			IsAnonymous = !poll.flags.HasFlag(TL.Poll.Flags.public_voters),
			Type = poll.flags.HasFlag(TL.Poll.Flags.quiz) ? "quiz" : "regular",
			AllowsMultipleAnswers = poll.flags.HasFlag(TL.Poll.Flags.multiple_choice),
			CorrectOptionId = correctOption < 0 ? null : correctOption,
			Explanation = pollResults.solution,
			ExplanationEntities = pollResults.solution_entities,
			OpenPeriod = poll.close_period == default ? null : poll.close_period,
			CloseDate = poll.close_date == default ? null : poll.close_date
		};
	}
}
